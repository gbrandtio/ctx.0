using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using CtxApp.Application.Abstractions;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// Builds a user's export bundle from every <see cref="IPersonalDataContributor"/>
/// registered in this workspace, so the answer covers exactly the features that
/// are enabled. The archive holds <c>export.json</c> — one section per contributor,
/// ordered by section name so two exports of the same data are byte-comparable —
/// plus any files contributed through <see cref="IPersonalDataAttachments"/>.
/// </summary>
public sealed class PersonalDataExporter(
    IEnumerable<IPersonalDataContributor> contributors,
    IEnumerable<IPersonalDataAttachments> attachments,
    IClock clock)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public async Task<byte[]> BuildArchiveAsync(Guid userId, CancellationToken ct = default)
    {
        var sections = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var contributor in contributors)
        {
            sections[contributor.Section] = await contributor.ExportAsync(userId, ct);
        }

        var bundle = new
        {
            GeneratedAt = clock.UtcNow,
            Subject = new { UserId = userId },
            Sections = sections,
        };

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            await using (var entry = archive.CreateEntry("export.json").Open())
            {
                await JsonSerializer.SerializeAsync(entry, bundle, Json, ct);
            }

            foreach (var source in attachments)
            {
                await foreach (var attachment in source.AttachmentsAsync(userId, ct))
                {
                    await using var content = await attachment.Open(ct);
                    await using var entry = archive.CreateEntry(attachment.Path).Open();
                    await content.CopyToAsync(entry, ct);
                }
            }
        }

        return buffer.ToArray();
    }
}
