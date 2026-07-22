using CtxApp.Application.Abstractions;
using CtxApp.Application.Security;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;
using CtxApp.Domain.Security;

namespace CtxApp.Application.Auth;

public sealed record RegisterResult(bool Success, string? Error, AuthTokens? Tokens);
public sealed record LoginResult(bool Success, string? Error, AuthTokens? Tokens);
public sealed record UserDto(Guid Id, string Email);

public interface IAuthService
{
    Task<RegisterResult> RegisterAsync(string email, string password, CancellationToken ct = default);
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);
    Task<UserDto?> GetUserAsync(Guid id, CancellationToken ct = default);
}
