using CtxApp.Domain.Profile;

namespace CtxApp.Application.Profile;

public interface IProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Add(UserProfile profile);
}
