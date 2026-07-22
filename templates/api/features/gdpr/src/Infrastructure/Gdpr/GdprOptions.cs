using Microsoft.Extensions.Configuration;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>Configuration for the privacy feature, read from the <c>GDPR_*</c> environment variables.</summary>
public sealed class GdprOptions
{
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

    /// <summary>Read the options from the <c>GDPR_*</c> environment variables, falling back to defaults.</summary>
    public static GdprOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new GdprOptions();
        return new GdprOptions
        {
            ExportRoot = configuration["GDPR_EXPORT_ROOT"] ?? defaults.ExportRoot,
            ExportTtlHours = int.TryParse(configuration["GDPR_EXPORT_TTL_HOURS"], out var hours) ? hours : defaults.ExportTtlHours,
            PolicyVersion = configuration["GDPR_POLICY_VERSION"] ?? defaults.PolicyVersion,
        };
    }
}
