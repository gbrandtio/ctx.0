using Acme.Application.Abstractions;
using Acme.Domain.Entities;
using Acme.Infrastructure.Security.Envelope;
using Microsoft.EntityFrameworkCore;

namespace Acme.Infrastructure.Persistence;

/// <summary>
/// EF Core (code-first) context for Acme on PostgreSQL. Properties marked
/// <c>[Encrypted]</c> are envelope-encrypted transparently via the security plane.
/// </summary>
public class AcmeDbContext(DbContextOptions<AcmeDbContext> options, IFieldCipher fieldCipher) : DbContext(options)
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
        modelBuilder.ApplyConfiguration(new Acme.Infrastructure.Persistence.Configurations.NoteConfiguration());
        modelBuilder.ApplyConfiguration(new Acme.Infrastructure.Persistence.Configurations.RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new Acme.Infrastructure.Persistence.Configurations.UserCredentialConfiguration());
        modelBuilder.ApplyCtxEncryption(fieldCipher);
        base.OnModelCreating(modelBuilder);
    }
}
