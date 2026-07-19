using CtxApp.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtxApp.Infrastructure.Persistence.Configurations;

public sealed class MediaObjectConfiguration : IEntityTypeConfiguration<MediaObject>
{
    public void Configure(EntityTypeBuilder<MediaObject> builder)
    {
        builder.ToTable("media");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).IsRequired();
        builder.Property(x => x.ContentType).IsRequired();
        builder.Property(x => x.StorageKey).IsRequired();
        builder.HasIndex(x => x.UserId);
    }
}
