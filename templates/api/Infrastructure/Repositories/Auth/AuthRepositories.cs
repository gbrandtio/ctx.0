using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Auth;

public sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);

    public Task RevokeAsync(long tokenId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);

    public Task RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.FamilyId == familyId && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);

    public Task RevokeAllForUserAsync(long userId, string userType, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.UserType == userType && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class SignupVerificationRepository(AppDbContext db, IClock clock)
    : ISignupVerificationRepository
{
    public Task<SignupVerification?> FindActiveByEmailHashAsync(
        string emailHash, CancellationToken ct) =>
        db.SignupVerifications
            .Where(v => v.EmailHash == emailHash &&
                        v.ConsumedAt == null &&
                        v.ExpiresAt > clock.UtcNow)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public void Add(SignupVerification verification) =>
        db.SignupVerifications.Add(verification);

    public void Remove(SignupVerification verification) =>
        db.SignupVerifications.Remove(verification);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class AppInstanceRepository(AppDbContext db) : IAppInstanceRepository
{
    public Task<AppInstance?> FindByDeviceIdAsync(string deviceId, CancellationToken ct) =>
        db.AppInstances.FirstOrDefaultAsync(a => a.DeviceId == deviceId, ct);

    public void Add(AppInstance instance) => db.AppInstances.Add(instance);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
