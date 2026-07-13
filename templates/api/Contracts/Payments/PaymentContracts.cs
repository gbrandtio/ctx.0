namespace Contracts.Payments;

/// <summary>
/// The client references a server-issued order by ID only; amount and
/// currency are read from the database (PAYMENTS_STRIPE.md §3).
/// </summary>
public sealed record CreatePaymentIntentRequest(long OrderId);

public sealed record PaymentIntentResponse(string ClientSecret);
