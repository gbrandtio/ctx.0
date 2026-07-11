using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.HasOne<OrgUser>().WithMany().HasForeignKey(o => o.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => o.OwnerId);
    }
}

public sealed class OrgUserConfiguration : IEntityTypeConfiguration<OrgUser>
{
    public void Configure(EntityTypeBuilder<OrgUser> builder)
    {
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.Email).HasColumnType("text").IsRequired(); // encrypted PII
        builder.Property(u => u.Name).HasColumnType("text");
        builder.Property(u => u.EncryptedDek).HasColumnType("text").IsRequired();
        builder.Property(u => u.EmailHash).HasMaxLength(64).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Type).HasMaxLength(20).IsRequired();
        builder.HasIndex(u => u.EmailHash).IsUnique();
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(p => p.Organization).WithMany(o => o.Projects)
            .HasForeignKey(p => p.OrgId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(p => p.Totals).WithOne()
            .HasForeignKey<ProjectTotals>(t => t.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MemberUserConfiguration : IEntityTypeConfiguration<MemberUser>
{
    public void Configure(EntityTypeBuilder<MemberUser> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Email).HasColumnType("text").IsRequired(); // encrypted PII
        builder.Property(m => m.Name).HasColumnType("text");
        builder.Property(m => m.EncryptedDek).HasColumnType("text").IsRequired();
        builder.Property(m => m.EmailHash).HasMaxLength(64).IsRequired();
        builder.Property(m => m.PasswordHash).HasMaxLength(100).IsRequired();
        builder.HasOne(m => m.Project).WithMany().HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(m => m.OrgId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(m => m.EmailHash).IsUnique();
        builder.HasIndex(m => m.ProjectId);
    }
}

public sealed class MemberInvitationConfiguration : IEntityTypeConfiguration<MemberInvitation>
{
    public void Configure(EntityTypeBuilder<MemberInvitation> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.EmailHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasOne(i => i.Project).WithMany().HasForeignKey(i => i.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Organization>().WithMany().HasForeignKey(i => i.OrgId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(i => i.EmailHash);
    }
}
