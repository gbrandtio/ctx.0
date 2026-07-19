namespace CtxApp.Application.Abstractions;

/// <summary>
/// Computes a deterministic keyed hash of a value so encrypted PII can still be
/// searched by exact match. The raw value never leaves the server in the clear;
/// queries filter on the blind index instead of the encrypted column.
/// </summary>
public interface IBlindIndex
{
    /// <summary>Deterministic keyed index for <paramref name="value"/> (case/space-normalized).</summary>
    string Compute(string value);
}
