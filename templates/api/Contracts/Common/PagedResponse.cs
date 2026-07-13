namespace Contracts.Common;

/// <summary>Cursorless page envelope; hasMore drives client infinite scroll.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, bool HasMore);
