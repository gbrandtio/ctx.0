using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class AppInstanceConfiguration : IEntityTypeConfiguration<AppInstance>
{
    public void Configure(EntityTypeBuilder<AppInstance> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.DeviceId).HasMaxLength(64).IsRequired();
        builder.Property(a => a.PublicKey).HasColumnType("text").IsRequired();
        builder.HasIndex(a => a.DeviceId).IsUnique();
    }
}

public sealed class SignupVerificationConfiguration
    : IEntityTypeConfiguration<SignupVerification>
{
    public void Configure(EntityTypeBuilder<SignupVerification> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();
        builder.Property(v => v.EmailHash).HasMaxLength(64).IsRequired();
        builder.Property(v => v.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(v => v.EmailHash);
    }
}

public sealed class UserGoogleIdentityConfiguration
    : IEntityTypeConfiguration<UserGoogleIdentity>
{
    public void Configure(EntityTypeBuilder<UserGoogleIdentity> builder)
    {
        builder.ToTable("user_google_identity"); // singular per DATABASE_RLS_POLICIES.md
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.GoogleSubjectHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(g => g.GoogleSubjectHash).IsUnique();
        builder.HasIndex(g => g.UserId).IsUnique();
    }
}

public sealed class UserFirebaseIdentityConfiguration
    : IEntityTypeConfiguration<UserFirebaseIdentity>
{
    public void Configure(EntityTypeBuilder<UserFirebaseIdentity> builder)
    {
        builder.ToTable("user_firebase_identity"); // singular per DATABASE_RLS_POLICIES.md
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.Token).HasColumnType("text").IsRequired(); // encrypted PII
        builder.Property(f => f.EncryptedDek).HasColumnType("text").IsRequired();
        builder.HasIndex(f => f.UserId).IsUnique();
    }
}
