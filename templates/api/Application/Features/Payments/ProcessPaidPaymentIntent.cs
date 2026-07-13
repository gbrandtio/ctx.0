using Application.Abstractions;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Payments;

public sealed record ProcessPaidPaymentIntentCommand(
    string PaymentIntentId,
    long OrderId,
    long UserId,
    long AmountMinor,
    string Currency) : IRequest;

/// <summary>
/// Invoked by the verified Stripe webhook (PAYMENTS_STRIPE.md §4):
/// re-validates against the server-side order, atomically consumes it
/// (single-use), blocks replays via the ledger's unique PaymentIntent
/// index, and writes the user notification through the outbox in the
/// same transaction scope.
/// </summary>
public sealed class ProcessPaidPaymentIntentHandler(
    IOrderRepository orders,
    ILedgerRepository ledger,
    INotificationRepository notifications,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<ProcessPaidPaymentIntentCommand>
{
    public async Task Handle(ProcessPaidPaymentIntentCommand command, CancellationToken ct)
    {
        // Replay/duplicate event guard.
        if (await ledger.PaymentIntentExistsAsync(command.PaymentIntentId, ct))
        {
            return;
        }

        var order = await orders.GetByIdAsync(command.OrderId, ct)
            ?? throw DomainException.NotFound("Order not found.");

        // Source-of-truth re-validation: the webhook amount must match the
        // order row exactly.
        if (order.AmountMinor != command.AmountMinor ||
            !string.Equals(order.Currency, command.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw DomainException.Conflict("Payment amount does not match the order.");
        }

        // Atomic single-use consumption; a second concurrent event loses.
        if (!await orders.TryMarkPaidAsync(
                order.Id, command.PaymentIntentId, command.UserId, ct))
        {
            return;
        }

        ledger.Add(new LedgerEntry
        {
            Id = ids.NextId(),
            UserId = command.UserId,
            OrderId = order.Id,
            StripePaymentIntentId = command.PaymentIntentId,
            AmountMinor = command.AmountMinor,
            Currency = command.Currency.ToUpperInvariant(),
            CreatedAt = clock.UtcNow,
        });
        await ledger.SaveChangesAsync(ct);

        notifications.Add(new UserNotification
        {
            Id = ids.NextId(),
            UserId = command.UserId,
            Type = "payment_completed",
            Title = "Payment completed",
            Body = "Your payment was processed successfully.",
            CreatedAt = clock.UtcNow,
        });
        await notifications.SaveChangesAsync(ct);
    }
}
