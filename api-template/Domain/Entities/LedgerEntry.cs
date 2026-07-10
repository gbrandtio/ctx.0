namespace Domain.Entities;

/// <summary>
/// Append-only payment ledger (PAYMENTS_STRIPE.md §4). The unique index
/// on stripe_payment_intent_id blocks replayed/duplicated webhook events.
/// </summary>
public class LedgerEntry
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long OrderId { get; set; }
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
