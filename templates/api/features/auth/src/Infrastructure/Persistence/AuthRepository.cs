using CtxApp.Application.Auth;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class AuthRepository(CtxAppDbContext db) : IAuthRepository
{
    public Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetUserByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Users.FindAsync(new object[] { id }, ct).AsTask();

    public Task<bool> IsEmailTakenAsync(string email, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == email, ct);

    public void AddUser(User user) => db.Users.Add(user);

    public Task<UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken ct = default) =>
        db.Set<UserCredential>().FindAsync(new object[] { userId }, ct).AsTask();

    public void AddUserCredential(UserCredential credential) =>
        db.Set<UserCredential>().Add(credential);
}
