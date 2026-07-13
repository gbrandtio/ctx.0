using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Ctx0.Security;

/// <summary>
/// ECDSA P-256/SHA-256 request-signature verification
/// (APPLICATION_LAYER_SECURITY.md §2). Accepts DER (ASN.1) and IEEE
/// P1363 signatures, and SubjectPublicKeyInfo or raw uncompressed
/// (0x04|X|Y) public keys, for cross-platform client compatibility.
/// </summary>
public static class EcdsaSignatureVerifier
{
    public static bool Verify(string publicKeyBase64, string canonicalPayload, string signatureBase64)
    {
        try
        {
            using var ecdsa = ImportPublicKey(Convert.FromBase64String(publicKeyBase64));
            var signature = NormalizeToP1363(Convert.FromBase64String(signatureBase64));
            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(canonicalPayload), signature, HashAlgorithmName.SHA256);
        }
        catch (Exception e) when (e is FormatException or CryptographicException or AsnContentException)
        {
            return false;
        }
    }

    private static ECDsa ImportPublicKey(byte[] keyBytes)
    {
        var ecdsa = ECDsa.Create();
        if (keyBytes is [0x04, ..] && keyBytes.Length == 65)
        {
            // Raw uncompressed point.
            ecdsa.ImportParameters(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = keyBytes[1..33],
                    Y = keyBytes[33..65],
                },
            });
        }
        else
        {
            ecdsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        }
        return ecdsa;
    }

    /// <summary>DER-encoded signatures are converted to 64-byte R|S.</summary>
    private static byte[] NormalizeToP1363(byte[] signature)
    {
        if (signature.Length == 64)
        {
            return signature; // already P1363
        }
        // Minimal DER SEQUENCE { INTEGER r, INTEGER s } parser.
        if (signature.Length < 8 || signature[0] != 0x30)
        {
            throw new CryptographicException("Unrecognized signature format.");
        }
        var offset = signature[1] == 0x81 ? 3 : 2;
        var r = ReadDerInteger(signature, ref offset);
        var s = ReadDerInteger(signature, ref offset);
        var result = new byte[64];
        r.CopyTo(result.AsSpan(32 - r.Length));
        s.CopyTo(result.AsSpan(64 - s.Length));
        return result;
    }

    private static byte[] ReadDerInteger(byte[] der, ref int offset)
    {
        if (der[offset] != 0x02)
        {
            throw new CryptographicException("Malformed DER signature.");
        }
        int length = der[offset + 1];
        offset += 2;
        var value = der.AsSpan(offset, length).ToArray();
        offset += length;
        // Strip the sign byte; reject integers wider than the curve.
        var trimmed = value.AsSpan(value.Length > 0 && value[0] == 0 ? 1 : 0).ToArray();
        return trimmed.Length <= 32
            ? trimmed
            : throw new CryptographicException("Integer exceeds curve size.");
    }
}

/// <summary>Marker exception type kept local to avoid a package reference.</summary>
public sealed class AsnContentException : Exception;
