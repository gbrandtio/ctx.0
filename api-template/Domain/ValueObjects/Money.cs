using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>
/// Monetary amount in minor units (cents) — integers only, no floating
/// point money. Currency is ISO 4217.
/// </summary>
public sealed record Money
{
    private Money(long amountMinor, string currency)
    {
        AmountMinor = amountMinor;
        Currency = currency;
    }

    public long AmountMinor { get; }
    public string Currency { get; }

    public static Money Create(long amountMinor, string currency)
    {
        if (amountMinor <= 0)
        {
            throw new DomainException("Amount must be positive.");
        }
        if (currency is not { Length: 3 })
        {
            throw new DomainException("Currency must be a 3-letter ISO 4217 code.");
        }
        return new Money(amountMinor, currency.ToUpperInvariant());
    }
}
