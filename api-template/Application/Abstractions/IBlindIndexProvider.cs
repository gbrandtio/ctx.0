namespace Application.Abstractions;

/// <summary>
/// HMAC-SHA256 blind index for searchable encrypted PII
/// (ENVELOPE_ENCRYPTION_ARCHITECTURE.md §2). Deterministic per input,
/// keyed with a dedicated index key distinct from the KEK.
/// </summary>
public interface IBlindIndexProvider
{
    string ComputeHash(string value);
}
