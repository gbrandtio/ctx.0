using System.Security.Cryptography;

namespace Ctx0.Security;

/// <summary>
/// Server side of hybrid ALE: RSA-OAEP-SHA256 unwrap of the per-request
/// session key + AES-256-GCM body encryption/decryption with the
/// Nonce(12)|Tag(16)|Ciphertext layout (APPLICATION_LAYER_SECURITY.md §1).
/// </summary>
public sealed class AleCryptoService
{
    private readonly RSA _rsa;

    public AleCryptoService(string rsaPrivateKeyPemOrPath)
    {
        var pem = LoadPem(rsaPrivateKeyPemOrPath);
        _rsa = RSA.Create();
        _rsa.ImportFromPem(pem);
    }

    public string PublicKeyPem =>
        "-----BEGIN PUBLIC KEY-----\n" +
        Convert.ToBase64String(
            _rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks) +
        "\n-----END PUBLIC KEY-----";

    public byte[] UnwrapSessionKey(string wrappedKeyBase64) =>
        _rsa.Decrypt(Convert.FromBase64String(wrappedKeyBase64), RSAEncryptionPadding.OaepSHA256);

    public static byte[] Decrypt(byte[] sessionKey, string base64Payload) =>
        AesEncryptionProvider.DecryptBytes(sessionKey, Convert.FromBase64String(base64Payload));

    public static string Encrypt(byte[] sessionKey, byte[] plaintext) =>
        Convert.ToBase64String(AesEncryptionProvider.EncryptBytes(sessionKey, plaintext));

    /// <summary>Supports raw PEM, file paths, and env vars with literal \n.</summary>
    public static string LoadPem(string value)
    {
        var normalized = value.Replace("\\n", "\n").Trim();
        if (normalized.Contains("-----BEGIN"))
        {
            return normalized;
        }
        return File.ReadAllText(value.Trim());
    }
}
