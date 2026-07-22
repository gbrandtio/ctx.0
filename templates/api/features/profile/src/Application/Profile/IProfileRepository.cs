using CtxApp.Domain.Profile;

namespace CtxApp.Application.Profile;

public interface IProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Add(UserProfile profile);
}
