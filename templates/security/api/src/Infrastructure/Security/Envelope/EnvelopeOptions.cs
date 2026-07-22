using Microsoft.Extensions.Configuration;

namespace CtxApp.Infrastructure.Security.Envelope;

/// <summary>
/// Envelope-encryption settings read from the <c>CTX_ENVELOPE_*</c> environment
/// variables. KEKs are versioned so they can be rotated: new values are wrapped
/// with <see cref="ActiveKekVersion"/>, while old versions stay available to
/// decrypt existing data.
/// </summary>
public sealed class EnvelopeOptions
{
    /// <summary>The environment-variable prefix for each versioned KEK (suffix is the version).</summary>
    public const string KeksPrefix = "CTX_ENVELOPE_KEKS_";

    /// <summary>KEK version -> base64 32-byte key.</summary>
    public Dictionary<string, string> Keks { get; init; } = new();

    /// <summary>Version used to wrap newly-encrypted values.</summary>
    public string ActiveKekVersion { get; init; } = "1";

    /// <summary>Base64 32-byte key for blind indexes.</summary>
    public string BlindIndexKey { get; init; } = string.Empty;

    /// <summary>
    /// Read the options from the <c>CTX_ENVELOPE_*</c> environment variables. Each
    /// <c>CTX_ENVELOPE_KEKS_&lt;version&gt;</c> variable contributes one versioned KEK.
    /// </summary>
    public static EnvelopeOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new EnvelopeOptions();
        var keks = new Dictionary<string, string>();
        foreach (var (key, value) in configuration.AsEnumerable())
        {
            if (value is not null && key.StartsWith(KeksPrefix, StringComparison.Ordinal))
            {
                keks[key[KeksPrefix.Length..]] = value;
            }
        }

        return new EnvelopeOptions
        {
            Keks = keks,
            ActiveKekVersion = configuration["CTX_ENVELOPE_ACTIVE_KEK_VERSION"] ?? defaults.ActiveKekVersion,
            BlindIndexKey = configuration["CTX_ENVELOPE_BLIND_INDEX_KEY"] ?? defaults.BlindIndexKey,
        };
    }
}
