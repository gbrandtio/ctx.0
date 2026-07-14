using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Users;

public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByEmailHashAsync(string emailHash, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.EmailHash == emailHash, ct);

    public Task<User?> FindByUsernameOrEmailHashAsync(string hash, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(
            u => u.EmailHash == hash || u.UsernameHash == hash, ct);

    public Task<User?> GetByIdAsync(long id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailHashExistsAsync(string emailHash, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.EmailHash == emailHash, ct);

    public void Add(User user) => db.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class GoogleIdentityRepository(AppDbContext db) : IGoogleIdentityRepository
{
    public Task<UserGoogleIdentity?> FindBySubjectHashAsync(
        string subjectHash, CancellationToken ct) =>
        db.UserGoogleIdentities.FirstOrDefaultAsync(
            g => g.GoogleSubjectHash == subjectHash, ct);

    public void Add(UserGoogleIdentity identity) => db.UserGoogleIdentities.Add(identity);

    public Task RemoveForUserAsync(long userId, CancellationToken ct) =>
        db.UserGoogleIdentities.Where(g => g.UserId == userId).ExecuteDeleteAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class FirebaseIdentityRepository(AppDbContext db) : IFirebaseIdentityRepository
{
    public Task<UserFirebaseIdentity?> FindByUserIdAsync(long userId, CancellationToken ct) =>
        db.UserFirebaseIdentities.FirstOrDefaultAsync(f => f.UserId == userId, ct);

    public void Add(UserFirebaseIdentity identity) => db.UserFirebaseIdentities.Add(identity);

    public void Remove(UserFirebaseIdentity identity) =>
        db.UserFirebaseIdentities.Remove(identity);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class UserExportRepository(AppDbContext db) : IUserExportRepository
{
    public void Add(UserExport export) => db.UserExports.Add(export);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
