namespace Acme.Domain.Entities;

/// <summary>
/// Core user entity. Rows are scoped per-tenant/owner by PostgreSQL RLS and
/// sensitive columns are envelope-encrypted at the Infrastructure layer.
/// Domain stays free of framework and persistence concerns.
/// </summary>
public sealed class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
