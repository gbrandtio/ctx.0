using System.Security.Cryptography;
using System.Text;

namespace Acme.Infrastructure.Security.Crypto;

/// <summary>One ALE-protected payload (base64 fields), serialized as JSON on the wire.</summary>
/// <remarks>
/// A request envelope carries the client's ephemeral public key (<see cref="Epk"/>);
/// a response envelope reuses the request's derived key, so <see cref="Epk"/> is null.
/// </remarks>
public sealed record AleEnvelope(string? Epk, string Iv, string Ct, string Tag);

/// <summary>
/// Application-Layer Encryption for ctx.0: ECIES over NIST P-256 with
/// AES-256-GCM. The sender derives a shared secret from its ephemeral private
/// key and the recipient's static public key (ECDH), runs it through
/// HKDF-SHA256, and seals the body with AES-256-GCM. The recipient derives the
/// same key from its static private key and the sender's ephemeral public key.
/// </summary>
public static class AleCipher
{
    private const int IvLength = 12;
    private const int TagLength = 16;
    private static readonly byte[] EmptyAad = Array.Empty<byte>();
    private static readonly byte[] ZeroSalt = new byte[32];

    /// <summary>ECDH + HKDF-SHA256 -> 32-byte AES key. Order of the two keys does not matter.</summary>
    public static byte[] DeriveKey(ECParameters ownPrivate, byte[] otherUncompressedPublic)
    {
        using var ownEcdh = ECDiffieHellman.Create(ownPrivate);
        using var otherEcdh = ECDiffieHellman.Create(P256.PublicParams(otherUncompressedPublic));
        var sharedX = ownEcdh.DeriveRawSecretAgreement(otherEcdh.PublicKey);
        return HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            sharedX,
            outputLength: 32,
            salt: ZeroSalt,
            info: Encoding.UTF8.GetBytes(CtxProtocol.AleHkdfInfo));
    }

    /// <summary>Seal <paramref name="plaintext"/> under a derived key using the supplied IV.</summary>
    public static (byte[] Ct, byte[] Tag) Encrypt(byte[] key, byte[] iv, byte[] plaintext)
    {
        var ct = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using var gcm = new AesGcm(key, TagLength);
        gcm.Encrypt(iv, plaintext, ct, tag, EmptyAad);
        return (ct, tag);
    }

    /// <summary>Open a sealed payload; throws <see cref="CryptographicException"/> on tamper.</summary>
    public static byte[] Decrypt(byte[] key, byte[] iv, byte[] ct, byte[] tag)
    {
        var plaintext = new byte[ct.Length];
        using var gcm = new AesGcm(key, TagLength);
        gcm.Decrypt(iv, ct, tag, plaintext, EmptyAad);
        return plaintext;
    }

    /// <summary>Decrypt a request envelope with the recipient's static ALE private key.</summary>
    public static byte[] OpenRequest(AleEnvelope envelope, ECParameters ownStaticPrivate)
    {
        if (string.IsNullOrEmpty(envelope.Epk))
        {
            throw new CryptographicException("Request envelope is missing the ephemeral public key.");
        }
        var key = DeriveKey(ownStaticPrivate, Convert.FromBase64String(envelope.Epk));
        return Decrypt(
            key,
            Convert.FromBase64String(envelope.Iv),
            Convert.FromBase64String(envelope.Ct),
            Convert.FromBase64String(envelope.Tag));
    }

    /// <summary>Seal a response under the key already derived from the request's ephemeral key.</summary>
    public static AleEnvelope SealResponse(byte[] key, byte[] plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var (ct, tag) = Encrypt(key, iv, plaintext);
        return new AleEnvelope(
            Epk: null,
            Iv: Convert.ToBase64String(iv),
            Ct: Convert.ToBase64String(ct),
            Tag: Convert.ToBase64String(tag));
    }
}
