using CtxApp.Application.Abstractions;
using CtxApp.Domain.Profile;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// The profile feature's personal data. Display name and bio come back decrypted
/// (the envelope converters run on read), and the single row is dropped on erasure.
/// </summary>
public sealed class ProfilePersonalData(CtxAppDbContext db) : IPersonalDataContributor
{
    public string Section => "profile";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await db.Set<UserProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        return profile is null
            ? null
            : new { profile.DisplayName, profile.Bio, profile.AvatarUrl, profile.AvatarMediaId, profile.UpdatedAt };
    }

    public async Task EraseAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await db.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
        {
            db.Set<UserProfile>().Remove(profile);
        }
    }
}
