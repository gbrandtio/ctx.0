namespace Ctx0.Security.Abstractions;

/// <summary>
/// The wire-protocol contract shared with the mobile security plane
/// (ctx0_mobile_security on pub.dev). The two packages are compatible iff
/// their protocol major.minor match; `ctx0 doctor` verifies this and the
/// API advertises it in the <see cref="HeaderName"/> response header.
/// Bump it whenever the signing string, ALE scheme, or security headers
/// change.
/// </summary>
public static class CtxProtocol
{
    public const string Version = "1.0";
    public const string HeaderName = "X-Ctx-Protocol";
}

/// <summary>
/// JWT claim names used across token issuance, authorization handlers,
/// and RLS identity propagation.
/// </summary>
public static class CtxClaimTypes
{
    public const string UserId = "uid";
    public const string OrgId = "orgId";
    public const string ProjectId = "projectId";
    public const string UserType = "type";
}

/// <summary>
/// Marks a string property as PII under envelope encryption: the
/// EnvelopeEncryptionInterceptor (Ctx0.Security.EfCore) encrypts it before
/// save and decrypts it on materialization. The owning entity must also
/// expose a string <c>EncryptedDek</c> property holding the wrapped
/// per-row DEK. Annotating the property IS the registration — there is no
/// central registry.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CtxEncryptedAttribute : Attribute;
