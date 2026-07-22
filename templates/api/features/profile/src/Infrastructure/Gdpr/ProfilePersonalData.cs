using CtxApp.Application.Abstractions;
using CtxApp.Domain.Profile;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// The profile feature's personal data. Display name and bio come back decrypted
/// (the envelope converters run on read), and the single row is dropped on erasure.
/// </summary>
public sealed class ProfilePersonalData(CtxAppDbContext dbContext) : IPersonalDataContributor
{
    public string Section => "profile";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.Set<UserProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return profile is null
            ? null
            : new { profile.DisplayName, profile.Bio, profile.AvatarUrl, profile.AvatarMediaId, profile.UpdatedAt };
    }

    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            dbContext.Set<UserProfile>().Remove(profile);
        }
    }
}
