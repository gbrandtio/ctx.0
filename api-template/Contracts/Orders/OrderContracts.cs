namespace Contracts.Orders;

public sealed record CreateOrderRequest(long AmountMinor, string Currency);

public sealed record OrderResponse(
    long Id,
    long ProjectId,
    long AmountMinor,
    string Currency,
    string Status,
    DateTime CreatedAt);
