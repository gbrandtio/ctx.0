namespace CtxApp.Infrastructure.Security.Crypto;

/// <summary>
/// Constants that define the ctx.0 wire protocol. The Flutter client and this
/// API implement the same values; both test suites assert against the shared
/// golden vectors in <c>protocol/vectors.json</c>.
/// </summary>
public static class CtxProtocol
{
    /// <summary>Protocol version advertised in the <c>X-Ctx-Protocol</c> header.</summary>
    public const string Version = "1.0";

    // Request headers.
    public const string ProtocolHeader = "X-Ctx-Protocol";
    public const string DeviceIdHeader = "X-Ctx-Device-Id";
    public const string TimestampHeader = "X-Ctx-Timestamp";
    public const string SignatureHeader = "X-Ctx-Signature";

    /// <summary>HKDF <c>info</c> string binding derived keys to this scheme/version.</summary>
    public const string AleHkdfInfo = "ctx-ale-v1";

    /// <summary>Maximum accepted clock skew for a signed request.</summary>
    public static readonly TimeSpan SignatureWindow = TimeSpan.FromMinutes(5);
}
