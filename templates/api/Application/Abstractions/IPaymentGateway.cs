namespace Application.Abstractions;

public interface IPaymentGateway
{
    /// <summary>
    /// Creates (or idempotently returns) a PaymentIntent for a server-side
    /// order. Amount/currency come from the order row; the idempotency key
    /// is derived from the order id — payment-intent:{orderId}
    /// (PAYMENTS_STRIPE.md §3).
    /// </summary>
    Task<(string PaymentIntentId, string ClientSecret)> CreatePaymentIntentAsync(
        long orderId,
        long amountMinor,
        string currency,
        long userId,
        long projectId,
        CancellationToken ct);
}
