using Domain.Exceptions;
using Domain.ValueObjects;
using Xunit;

namespace UnitTests.Domain;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData("john@example.com")]
    [InlineData("  John@Example.COM  ")]
    public void Email_normalizes_valid_addresses(string raw)
    {
        // Canonical lowercasing is what makes the blind index deterministic.
        Assert.Equal("john@example.com", Email.Create(raw).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@domain")]
    public void Email_rejects_invalid_addresses(string raw) =>
        Assert.Throws<DomainException>(() => Email.Create(raw));

    [Fact]
    public void Password_enforces_minimum_length() =>
        Assert.Throws<DomainException>(() => Password.Create("short"));

    [Fact]
    public void Money_rejects_non_positive_amounts() =>
        Assert.Throws<DomainException>(() => Money.Create(0, "EUR"));

    [Fact]
    public void Money_uppercases_currency() =>
        Assert.Equal("EUR", Money.Create(500, "eur").Currency);
}
