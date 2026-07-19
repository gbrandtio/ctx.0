using CtxApp.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtxApp.Infrastructure.Persistence.Configurations;

public sealed class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Platform).IsRequired();
        builder.Property(x => x.Token).IsRequired();
        builder.Property(x => x.TokenBlindIndex).IsRequired();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TokenBlindIndex);
    }
}
