namespace Contracts.Security;

/// <summary>GET /v1/security/metadata payload (APPLICATION_LAYER_SECURITY.md §3).</summary>
public sealed record SecurityMetadataResponse(
    bool AleEnabled,
    string AlePublicKey,
    bool RequestSigningRequired,
    int SignatureWindowSeconds,
    IReadOnlyList<string> SupportedAttestationTypes);

public sealed record RegisterAppInstanceRequest(string DeviceId, string PublicKey);
