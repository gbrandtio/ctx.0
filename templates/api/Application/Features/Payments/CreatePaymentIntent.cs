using Application.Abstractions;
using Contracts.Payments;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Payments;

public sealed record CreatePaymentIntentCommand(long UserId, long OrderId)
    : IRequest<PaymentIntentResponse>;

/// <summary>
/// Consumer actor pays against a server-issued order. Amount and currency
/// come from the database row — a client claiming a different amount is
/// structurally impossible (PAYMENTS_STRIPE.md §3).
/// </summary>
public sealed class CreatePaymentIntentHandler(
    IOrderRepository orders,
    IPaymentGateway payments) : IRequestHandler<CreatePaymentIntentCommand, PaymentIntentResponse>
{
    public async Task<PaymentIntentResponse> Handle(
        CreatePaymentIntentCommand command, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(command.OrderId, ct)
            ?? throw DomainException.NotFound("Order not found.");
        order.EnsurePayable();

        var (paymentIntentId, clientSecret) = await payments.CreatePaymentIntentAsync(
            order.Id, order.AmountMinor, order.Currency,
            command.UserId, order.ProjectId, ct);

        order.StripePaymentIntentId = paymentIntentId;
        await orders.SaveChangesAsync(ct);

        return new PaymentIntentResponse(clientSecret);
    }
}
