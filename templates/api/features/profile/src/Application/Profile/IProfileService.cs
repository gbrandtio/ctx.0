namespace CtxApp.Application.Profile;

public sealed record ProfileDto(string DisplayName, string? Bio, string? AvatarUrl, Guid? AvatarMediaId, DateTimeOffset UpdatedAt);

public interface IProfileService
{
    Task<ProfileDto> GetOrCreateProfileAsync(Guid userId, CancellationToken ct = default);
    Task<ProfileDto> UpdateProfileAsync(Guid userId, string? displayName, string? bio, string? avatarUrl, Guid? avatarMediaId, CancellationToken ct = default);
}
