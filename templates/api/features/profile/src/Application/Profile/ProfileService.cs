using CtxApp.Application.Abstractions;
using CtxApp.Domain.Profile;

namespace CtxApp.Application.Profile;

public sealed class ProfileService(IProfileRepository profiles, IUnitOfWork unitOfWork) : IProfileService
{
    public async Task<ProfileDto> GetOrCreateProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await profiles.GetByUserIdAsync(userId, ct);
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId, DisplayName = string.Empty };
            profiles.Add(profile);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return Present(profile);
    }

    public async Task<ProfileDto> UpdateProfileAsync(Guid userId, string? displayName, string? bio, string? avatarUrl, Guid? avatarMediaId, CancellationToken ct = default)
    {
        var profile = await profiles.GetByUserIdAsync(userId, ct);
        if (profile is null)
        {
            profile = new UserProfile { UserId = userId, DisplayName = displayName ?? string.Empty };
            profiles.Add(profile);
        }
        else if (displayName is not null)
        {
            profile.DisplayName = displayName;
        }

        profile.Bio = bio;
        profile.AvatarUrl = avatarUrl;
        profile.AvatarMediaId = avatarMediaId;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        
        await unitOfWork.SaveChangesAsync(ct);
        return Present(profile);
    }

    private static ProfileDto Present(UserProfile p) =>
        new ProfileDto(p.DisplayName, p.Bio, p.AvatarUrl, p.AvatarMediaId, p.UpdatedAt);
}
