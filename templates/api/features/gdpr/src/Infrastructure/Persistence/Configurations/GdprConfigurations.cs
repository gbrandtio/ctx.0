using CtxApp.Domain.Gdpr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtxApp.Infrastructure.Persistence.Configurations;

public sealed class ConsentRecordConfiguration : IEntityTypeConfiguration<ConsentRecord>
{
    public void Configure(EntityTypeBuilder<ConsentRecord> builder)
    {
        builder.ToTable("consent_records");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PolicyVersion).IsRequired();
        builder.Property(x => x.Purposes).IsRequired();
        builder.Property(x => x.Source).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.DecidedAt });
    }
}

public sealed class DataExportJobConfiguration : IEntityTypeConfiguration<DataExportJob>
{
    public void Configure(EntityTypeBuilder<DataExportJob> builder)
    {
        builder.ToTable("data_export_jobs");
        builder.HasKey(x => x.Id);
        // Stored as its name so the table stays readable to an operator auditing it.
        builder.Property(x => x.Status).HasConversion<string>().IsRequired();
        builder.Property(x => x.StorageKey).IsRequired();
        builder.Property(x => x.DownloadTokenHash).IsRequired();
        builder.HasIndex(x => x.UserId);
    }
}
