using Domain.Exceptions;

namespace Domain.Entities;

/// <summary>
/// Server-issued, single-use order record — the authoritative source of
/// amount/currency for payments (PAYMENTS_STRIPE.md). Created by a member
/// actor, paid by a consumer actor, atomically invalidated on fulfillment.
/// </summary>
public class Order
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long CreatedByMemberUserId { get; set; }

    /// <summary>Minor units (cents). The client NEVER supplies this at payment time.</summary>
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;

    public string Status { get; set; } = Statuses.Pending;
    public string? StripePaymentIntentId { get; set; }
    public long? PaidByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? InvalidatedAt { get; set; }

    public Project? Project { get; set; }

    public static class Statuses
    {
        public const string Pending = "pending";
        public const string Paid = "paid";
        public const string Invalidated = "invalidated";
    }

    public void EnsurePayable()
    {
        if (Status != Statuses.Pending)
        {
            throw DomainException.Conflict("Order is no longer payable.");
        }
    }
}
