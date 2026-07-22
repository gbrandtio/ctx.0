namespace CtxApp.Application.Abstractions;

/// <summary>A file carried in an export bundle, under <paramref name="Path"/> inside the archive.</summary>
/// <param name="Path">Archive-relative path, e.g. "media/2f1c…-holiday.jpg".</param>
/// <param name="Open">Opens the content; the caller disposes the returned stream.</param>
public sealed record PersonalDataAttachment(string Path, Func<CancellationToken, Task<Stream>> Open);

/// <summary>
/// Implemented alongside <see cref="IPersonalDataContributor"/> by features whose
/// personal data is not only JSON — stored files, for instance. The exporter
/// streams each attachment into the archive next to the JSON bundle.
/// </summary>
public interface IPersonalDataAttachments
{
    IAsyncEnumerable<PersonalDataAttachment> AttachmentsAsync(Guid userId, CancellationToken cancellationToken = default);
}
