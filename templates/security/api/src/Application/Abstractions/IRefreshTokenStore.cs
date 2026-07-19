using CtxApp.Domain.Security;

namespace CtxApp.Application.Abstractions;

/// <summary>Persistence for refresh tokens and their rotation families.</summary>
public interface IRefreshTokenStore
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Revoke every token in a family (used on reuse detection / logout).</summary>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
