namespace Ctx0.Security;

/// <summary>
/// Disables ALE for an endpoint (streaming/webhook routes —
/// APPLICATION_LAYER_SECURITY.md §1, ADR-0003). Attach with
/// .WithMetadata(new AllowPlaintextAttribute()).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
public sealed class AllowPlaintextAttribute : Attribute;

/// <summary>
/// Disables ECDSA request-signature verification for an endpoint
/// (registration, metadata, webhooks, SSE — APPLICATION_LAYER_SECURITY.md §2).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
public sealed class SkipRequestSigningAttribute : Attribute;
