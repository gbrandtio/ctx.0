namespace CtxApp.Application.Abstractions;

/// <summary>
/// Envelope encryption for individual field values: a per-value data key (DEK)
/// encrypts the value, and the DEK is wrapped by a versioned key-encryption key
/// (KEK). The encoded envelope carries the KEK version so keys can be rotated.
/// </summary>
public interface IFieldCipher
{
    /// <summary>Encrypt a plaintext value into a self-describing envelope string.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypt an envelope produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string envelope);
}
