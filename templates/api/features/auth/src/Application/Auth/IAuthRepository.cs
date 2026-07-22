using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;

namespace CtxApp.Application.Auth;

public interface IAuthRepository
{
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> IsEmailTakenAsync(string email, CancellationToken ct = default);
    void AddUser(User user);

    Task<UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken ct = default);
    void AddUserCredential(UserCredential credential);
}
