using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Password complexity rules, enforced at the deepest level.</summary>
public sealed record Password
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    private Password(string value) => Value = value;

    public string Value { get; }

    public static Password Create(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < MinLength)
        {
            throw new DomainException($"Password must be at least {MinLength} characters.");
        }
        if (raw.Length > MaxLength)
        {
            throw new DomainException($"Password must be at most {MaxLength} characters.");
        }
        return new Password(raw);
    }
}
