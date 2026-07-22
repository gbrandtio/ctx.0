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
public sealed class AuthPersonalData(CtxAppDbContext dbContext) : IPersonalDataContributor
{
    public string Section => "account";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var sessions = await dbContext.Set<RefreshToken>()
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new { t.CreatedAt, t.ExpiresAt, t.RevokedAt })
            .ToListAsync(cancellationToken);

        return new
        {
            user.Id,
            user.Email,
            user.CreatedAt,
            HasPassword = await dbContext.Set<UserCredential>().AnyAsync(c => c.UserId == userId, cancellationToken),
            Sessions = sessions,
        };
    }

    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await dbContext.Set<RefreshToken>().Where(t => t.UserId == userId).ToListAsync(cancellationToken);
        dbContext.Set<RefreshToken>().RemoveRange(tokens);

        var credential = await dbContext.Set<UserCredential>().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (credential is not null)
        {
            dbContext.Set<UserCredential>().Remove(credential);
        }
    }
}
