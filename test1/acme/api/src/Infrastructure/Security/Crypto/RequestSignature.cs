using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Acme.Infrastructure.Security.Crypto;

/// <summary>
/// ECDSA P-256 request signing for ctx.0. The signature covers a canonical
/// string built from the method, path+query, timestamp, and a SHA-256 hash of
/// the exact body bytes on the wire (the ALE envelope). Signatures use the
/// IEEE P1363 fixed-width (r||s) encoding, base64.
/// </summary>
public static class RequestSignature
{
    /// <summary>Build the canonical string that is signed and verified.</summary>
    public static string Canonical(string method, string pathAndQuery, string timestamp, byte[] body)
    {
        var bodyHash = Convert.ToHexStringLower(SHA256.HashData(body));
        return string.Join('\n', method.ToUpperInvariant(), pathAndQuery, timestamp, bodyHash);
    }

    /// <summary>Sign the canonical string with a device private key. Returns base64(P1363).</summary>
    public static string Sign(ECParameters devicePrivate, string method, string pathAndQuery, string timestamp, byte[] body)
    {
        using var ecdsa = ECDsa.Create(devicePrivate);
        var canonical = Encoding.UTF8.GetBytes(Canonical(method, pathAndQuery, timestamp, body));
        var sig = ecdsa.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String(sig);
    }

    /// <summary>Verify a base64(P1363) signature against a device public key.</summary>
    public static bool Verify(
        byte[] deviceUncompressedPublic,
        string signatureB64,
        string method,
        string pathAndQuery,
        string timestamp,
        byte[] body)
    {
        using var ecdsa = ECDsa.Create(P256.PublicParams(deviceUncompressedPublic));
        var canonical = Encoding.UTF8.GetBytes(Canonical(method, pathAndQuery, timestamp, body));
        byte[] sig;
        try
        {
            sig = Convert.FromBase64String(signatureB64);
        }
        catch (FormatException)
        {
            return false;
        }
        return ecdsa.VerifyData(canonical, sig, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>Whether a request timestamp (unix ms) is within the accepted window of now.</summary>
    public static bool TimestampFresh(string timestamp, DateTimeOffset now)
    {
        if (!long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
        {
            return false;
        }
        var sent = DateTimeOffset.FromUnixTimeMilliseconds(ms);
        var delta = (now - sent).Duration();
        return delta <= CtxProtocol.SignatureWindow;
    }
}
