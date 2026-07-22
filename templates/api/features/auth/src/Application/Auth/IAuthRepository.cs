using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;

namespace CtxApp.Application.Auth;

public interface IAuthRepository
{
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken = default);
    void AddUser(User user);

    Task<UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken cancellationToken = default);
    void AddUserCredential(UserCredential credential);
}
