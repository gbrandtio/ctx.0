namespace Acme.Infrastructure.Security.Envelope;

/// <summary>
/// Envelope-encryption settings bound from the <c>Ctx:Envelope</c> configuration
/// section. KEKs are versioned so they can be rotated: new values are wrapped
/// with <see cref="ActiveKekVersion"/>, while old versions stay available to
/// decrypt existing data.
/// </summary>
public sealed class EnvelopeOptions
{
    public const string Section = "Ctx:Envelope";

    /// <summary>KEK version -> base64 32-byte key.</summary>
    public Dictionary<string, string> Keks { get; init; } = new();

    /// <summary>Version used to wrap newly-encrypted values.</summary>
    public string ActiveKekVersion { get; init; } = "1";

    /// <summary>Base64 32-byte key for blind indexes.</summary>
    public string BlindIndexKey { get; init; } = string.Empty;
}
