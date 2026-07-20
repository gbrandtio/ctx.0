namespace CtxApp.Application.Abstractions;

/// <summary>
/// One feature's view of the personal data it holds for a user: what to hand
/// back on a subject access / portability request, and what to erase on account
/// deletion. Every feature that stores user-owned rows registers an
/// implementation, so the <c>gdpr</c> feature can serve both requests without
/// knowing which features are enabled in this workspace.
/// </summary>
public interface IPersonalDataContributor
{
    /// <summary>Key this contributor's data appears under in the export bundle, e.g. "notes".</summary>
    string Section { get; }

    /// <summary>
    /// The user's data for this feature, shaped for a human-readable export.
    /// Return null when the feature holds nothing for the user. Derived and
    /// internal values (hashes, blind indexes, foreign keys) are left out.
    /// </summary>
    Task<object?> ExportAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Erase everything this feature holds for the user. Called inside the
    /// deletion transaction, so implementations stage their deletes on the
    /// shared <c>DbContext</c> and leave committing to the caller.
    /// </summary>
    Task EraseAsync(Guid userId, CancellationToken ct = default);
}
