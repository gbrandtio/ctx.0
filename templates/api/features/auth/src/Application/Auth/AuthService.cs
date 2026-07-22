using CtxApp.Application.Abstractions;
using CtxApp.Application.Security;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;

namespace CtxApp.Application.Auth;

public sealed class AuthService(
    IAuthRepository repository,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    RefreshTokenService tokens) : IAuthService
{
    public async Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || password.Length < 8)
        {
            return new RegisterResult(false, "credentialsRequired", null);
        }

        if (await repository.IsEmailTakenAsync(email, cancellationToken))
        {
            return new RegisterResult(false, "emailTaken", null);
        }

        var user = new User { Email = email };
        repository.AddUser(user);
        repository.AddUserCredential(new UserCredential { UserId = user.Id, PasswordHash = hasher.Hash(password) });
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var authTokens = await tokens.IssueAsync(user.Id, cancellationToken);
        return new RegisterResult(true, null, authTokens);
    }

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetUserByEmailAsync(email, cancellationToken);
        var credential = user is null ? null : await repository.GetUserCredentialAsync(user.Id, cancellationToken);

        if (user is null || credential is null || !hasher.Verify(password, credential.PasswordHash))
        {
            return new LoginResult(false, "invalidCredentials", null);
        }

        var authTokens = await tokens.IssueAsync(user.Id, cancellationToken);
        return new LoginResult(true, null, authTokens);
    }

    public async Task<UserDto?> GetUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await repository.GetUserByIdAsync(id, cancellationToken);
        return user is null ? null : new UserDto(user.Id, user.Email);
    }
}
