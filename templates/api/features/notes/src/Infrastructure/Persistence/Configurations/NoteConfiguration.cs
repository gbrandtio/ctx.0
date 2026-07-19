using CtxApp.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CtxApp.Infrastructure.Persistence.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.ToTable("notes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.TitleBlindIndex).IsRequired();
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TitleBlindIndex);
    }
}
