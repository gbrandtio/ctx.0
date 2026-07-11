using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever(); // snowflake

        // Encrypted PII: ciphertext is Base64 text of unbounded length.
        builder.Property(u => u.Username).HasColumnType("text").IsRequired();
        builder.Property(u => u.Email).HasColumnType("text").IsRequired();
        builder.Property(u => u.Name).HasColumnType("text");
        builder.Property(u => u.EncryptedDek).HasColumnType("text").IsRequired();

        builder.Property(u => u.UsernameHash).HasMaxLength(64).IsRequired();
        builder.Property(u => u.EmailHash).HasMaxLength(64).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(100);

        // Anonymized rows clear their hashes, so uniqueness must ignore
        // the empty sentinel.
        builder.HasIndex(u => u.EmailHash).IsUnique()
            .HasFilter("email_hash <> ''");
        builder.HasIndex(u => u.UsernameHash).IsUnique()
            .HasFilter("username_hash <> ''");

        builder.HasOne(u => u.GoogleIdentity).WithOne()
            .HasForeignKey<UserGoogleIdentity>(g => g.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(u => u.FirebaseIdentity).WithOne()
            .HasForeignKey<UserFirebaseIdentity>(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(u => u.Totals).WithOne()
            .HasForeignKey<UserTotals>(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
