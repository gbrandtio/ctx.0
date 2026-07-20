namespace CtxApp.Infrastructure.Gdpr;

/// <summary>Configuration for the privacy feature, bound from the <c>Gdpr</c> section.</summary>
public sealed class GdprOptions
{
    public const string Section = "Gdpr";

    /// <summary>Filesystem directory holding encrypted export archives.</summary>
    public string ExportRoot { get; set; } = "./_exports";

    /// <summary>How long a completed export stays downloadable. One week by default.</summary>
    public int ExportTtlHours { get; set; } = 168;

    /// <summary>
    /// The privacy-notice version currently in force. The app re-prompts whenever
    /// the user's recorded consent names a different version, so bumping this is
    /// how a changed notice is re-consented.
    /// </summary>
    public string PolicyVersion { get; set; } = "1";

    public TimeSpan ExportTtl => TimeSpan.FromHours(ExportTtlHours);
}
