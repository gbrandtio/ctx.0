namespace Acme.Domain.Security;

/// <summary>
/// Marks a string property whose value is envelope-encrypted at rest. The
/// security plane applies a transparent EF value converter to every property
/// carrying this attribute, so entities always see plaintext in memory while the
/// database column holds ciphertext.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute;
