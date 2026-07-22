using CtxApp.Application.Auth;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class AuthRepository(CtxAppDbContext dbContext) : IAuthRepository
{
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Users.FindAsync(new object[] { id }, cancellationToken).AsTask();

    public Task<bool> IsEmailTakenAsync(string email, CancellationToken cancellationToken = default) =>
        dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public void AddUser(User user) => dbContext.Users.Add(user);

    public Task<UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Set<UserCredential>().FindAsync(new object[] { userId }, cancellationToken).AsTask();

    public void AddUserCredential(UserCredential credential) =>
        dbContext.Set<UserCredential>().Add(credential);
}
