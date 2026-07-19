using CtxApp.Application.Abstractions;
using CtxApp.Domain.Entities;
using CtxApp.Infrastructure.Security.Envelope;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

/// <summary>
/// EF Core (code-first) context for CtxApp on PostgreSQL. Properties marked
/// <c>[Encrypted]</c> are envelope-encrypted transparently via the security plane.
/// </summary>
public class CtxAppDbContext(DbContextOptions<CtxAppDbContext> options, IFieldCipher fieldCipher) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
        });
        // ctx:anchor:model-config
        modelBuilder.ApplyCtxEncryption(fieldCipher);
        base.OnModelCreating(modelBuilder);
    }
}
