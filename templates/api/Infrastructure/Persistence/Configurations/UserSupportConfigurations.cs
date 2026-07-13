using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasMaxLength(50).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasColumnType("text").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Feed paging + the worker's pending scan (sent_at IS NULL).
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => n.SentAt).HasFilter("sent_at IS NULL");
    }
}

public sealed class UserExportConfiguration : IEntityTypeConfiguration<UserExport>
{
    public void Configure(EntityTypeBuilder<UserExport> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Status).HasMaxLength(20).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.Status);
    }
}

public sealed class UserTotalsConfiguration : IEntityTypeConfiguration<UserTotals>
{
    public void Configure(EntityTypeBuilder<UserTotals> builder)
    {
        builder.HasKey(t => t.UserId);
        builder.Property(t => t.UserId).ValueGeneratedNever();
    }
}
