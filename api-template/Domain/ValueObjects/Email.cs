using System.Text.RegularExpressions;
using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Validated email address (lowercased for canonical hashing).</summary>
public sealed partial record Email
{
    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string raw)
    {
        var value = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        if (value.Length is 0 or > 320 || !EmailRegex().IsMatch(value))
        {
            throw new DomainException("A valid email address is required.");
        }
        return new Email(value);
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    public override string ToString() => Value;
}
