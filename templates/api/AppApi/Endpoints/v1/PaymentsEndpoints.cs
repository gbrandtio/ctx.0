using System.Security.Claims;
using AppApi.Endpoints;
using AppApi.Middleware;
using Application.Features.Payments;
using Contracts.Payments;
using Ctx0.Security;
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
                IMediator mediator, AppDbContext db,
                CurrentUserContext currentUser, CancellationToken ct) =>
            {
                var userId = long.Parse(
                    user.FindFirstValue(SecurityConstants.ClaimTypes.UserId)!);

                // A consumer is a `users` identity, not a project member, so
                // the order (owned by its project) is invisible under the
                // consumer's own RLS context. Authorization already happened
                // via the PaymentProcess policy above; the order lookup and
                // its intent-id write are a controlled server action, so they
                // run as the internal worker role inside a transaction — the
                // same posture as the webhook (C1).
                using (currentUser.BeginSystemBypassScope())
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    var response = await mediator.Send(
                        new CreatePaymentIntentCommand(userId, request.OrderId), ct);
                    await tx.CommitAsync(ct);
                    return Results.Ok(response);
                }
            })
            .RequireAuthorization(SecurityConstants.Policies.PaymentProcess);

        payments.MapPost("/stripe-webhook", async (
                HttpRequest httpRequest,
                IOptions<StripeOptions> stripeOptions,
                IMediator mediator,
                AppDbContext db,
                CurrentUserContext currentUser,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("PaymentsWebhook");
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

                if (stripeEvent.Type != EventTypes.PaymentIntentSucceeded ||
                    stripeEvent.Data.Object is not PaymentIntent intent)
                {
                    return Results.Ok(); // unrelated event — acknowledge and ignore
                }

                if (!long.TryParse(intent.Metadata.GetValueOrDefault("orderId"), out var orderId) ||
                    !long.TryParse(intent.Metadata.GetValueOrDefault("userId"), out var userId) ||
                    !long.TryParse(intent.Metadata.GetValueOrDefault("projectId"), out var projectId))
                {
                    // Acknowledge so Stripe stops retrying, but surface the
                    // anomaly — a succeeded intent we minted always carries
                    // these keys (StripePaymentGateway).
                    logger.LogWarning(
                        "payment_intent.succeeded {IntentId} missing/malformed order metadata; ignored.",
                        intent.Id);
                    return Results.Ok();
                }

                // The webhook is anonymous, so there is no JWT identity for
                // RLS. Fulfillment runs as the internal worker role inside a
                // single transaction: the order UPDATE, ledger INSERT and
                // notification INSERT commit together or not at all — a crash
                // mid-way rolls back and Stripe's redelivery retries cleanly
                // (C1/H1).
                bool marked;
                using (currentUser.BeginSystemBypassScope())
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    marked = await mediator.Send(new ProcessPaidPaymentIntentCommand(
                        intent.Id, orderId, userId, intent.Amount, intent.Currency), ct);
                    await tx.CommitAsync(ct);
                }

                // Multi-instance SSE fan-out (ADR-0003 §5) — exactly once, so
                // a replayed delivery never re-broadcasts (L4).
                if (marked)
                {
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
