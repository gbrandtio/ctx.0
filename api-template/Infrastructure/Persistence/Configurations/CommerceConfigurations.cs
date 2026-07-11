using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Currency).HasMaxLength(3).IsRequired();
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired();
        builder.Property(o => o.StripePaymentIntentId).HasMaxLength(255);
        builder.HasOne(o => o.Project).WithMany().HasForeignKey(o => o.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MemberUser>().WithMany().HasForeignKey(o => o.CreatedByMemberUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => new { o.ProjectId, o.CreatedAt });
        builder.HasIndex(o => o.Status);
    }
}

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Currency).HasMaxLength(3).IsRequired();
        builder.Property(l => l.StripePaymentIntentId).HasMaxLength(255).IsRequired();
        builder.HasOne<Order>().WithMany().HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The webhook replay guard (PAYMENTS_STRIPE.md §4).
        builder.HasIndex(l => l.StripePaymentIntentId).IsUnique();
    }
}

public sealed class ProjectTotalsConfiguration : IEntityTypeConfiguration<ProjectTotals>
{
    public void Configure(EntityTypeBuilder<ProjectTotals> builder)
    {
        builder.HasKey(t => t.ProjectId);
        builder.Property(t => t.ProjectId).ValueGeneratedNever();
    }
}

public sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Description).HasColumnType("text");

        // geography (meters), not geometry (degrees) — SPATIAL_QUERIES.md.
        builder.Property(i => i.Location).HasColumnType("geography (point)").IsRequired();
        builder.HasIndex(i => i.Location).HasMethod("GIST");
    }
}
