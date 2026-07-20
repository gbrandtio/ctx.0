using CtxApp.Application.Abstractions;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;
using CtxApp.Domain.Security;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// The auth feature's personal data: the account identity and its login sessions.
/// The password hash is never exported — it is a credential, not user content —
/// and erasure drops the credential row along with every refresh-token family.
/// </summary>
public sealed class AuthPersonalData(CtxAppDbContext db) : IPersonalDataContributor
{
    public string Section => "account";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return null;
        }

        var sessions = await db.Set<RefreshToken>()
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new { t.CreatedAt, t.ExpiresAt, t.RevokedAt })
            .ToListAsync(ct);

        return new
        {
            user.Id,
            user.Email,
            user.CreatedAt,
            HasPassword = await db.Set<UserCredential>().AnyAsync(c => c.UserId == userId, ct),
            Sessions = sessions,
        };
    }

    public async Task EraseAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await db.Set<RefreshToken>().Where(t => t.UserId == userId).ToListAsync(ct);
        db.Set<RefreshToken>().RemoveRange(tokens);

        var credential = await db.Set<UserCredential>().FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (credential is not null)
        {
            db.Set<UserCredential>().Remove(credential);
        }
    }
}
