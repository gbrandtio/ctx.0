using System.Security.Claims;
using AppApi.Endpoints;
using AppApi.Middleware;
using Application.Features.Payments;
using Contracts.Payments;
using Domain.Constants;
using Infrastructure.External;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace AppApi.Endpoints.v1;

/// <summary>
/// Consumer-actor payment + verified Stripe webhook (PAYMENTS_STRIPE.md).
/// The webhook bypasses ALE/signing (Stripe signs with its own scheme)
/// and re-validates everything against the server-side order row.
/// </summary>
public sealed class PaymentsEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder v1)
    {
        var payments = v1.MapGroup("/payments");

        payments.MapPost("/intents", async (
                CreatePaymentIntentRequest request, ClaimsPrincipal user,
                IMediator mediator, CancellationToken ct) =>
            {
                var userId = long.Parse(
                    user.FindFirstValue(SecurityConstants.ClaimTypes.UserId)!);
                return Results.Ok(await mediator.Send(
                    new CreatePaymentIntentCommand(userId, request.OrderId), ct));
            })
            .RequireAuthorization(SecurityConstants.Policies.PaymentProcess);

        payments.MapPost("/stripe-webhook", async (
                HttpRequest httpRequest,
                IOptions<StripeOptions> stripeOptions,
                IMediator mediator,
                AppDbContext db,
                CancellationToken ct) =>
            {
                var payload = await new StreamReader(httpRequest.Body).ReadToEndAsync(ct);
                Event stripeEvent;
                try
                {
                    // Signature verification against the endpoint secret
                    // (PAYMENTS_STRIPE.md §4 step 1).
                    stripeEvent = EventUtility.ConstructEvent(
                        payload,
                        httpRequest.Headers["Stripe-Signature"],
                        stripeOptions.Value.WebhookSecret);
                }
                catch (StripeException)
                {
                    return Results.BadRequest();
                }

                if (stripeEvent.Type == EventTypes.PaymentIntentSucceeded &&
                    stripeEvent.Data.Object is PaymentIntent intent &&
                    long.TryParse(intent.Metadata.GetValueOrDefault("orderId"), out var orderId) &&
                    long.TryParse(intent.Metadata.GetValueOrDefault("userId"), out var userId) &&
                    long.TryParse(intent.Metadata.GetValueOrDefault("projectId"), out var projectId))
                {
                    await mediator.Send(new ProcessPaidPaymentIntentCommand(
                        intent.Id, orderId, userId, intent.Amount, intent.Currency), ct);

                    // Multi-instance SSE fan-out (ADR-0003 §5).
                    await db.Database.ExecuteSqlAsync(
                        $"SELECT pg_notify('payment_completed', {System.Text.Json.JsonSerializer.Serialize(new { projectId, orderId, type = "payment_completed" })})",
                        ct);
                }

                return Results.Ok();
            })
            .WithMetadata(new AllowPlaintextAttribute())
            .WithMetadata(new SkipRequestSigningAttribute())
            .AllowAnonymous();
    }
}
