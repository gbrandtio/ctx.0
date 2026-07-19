using System.Security.Cryptography;

namespace Acme.Infrastructure.Security.Crypto;

/// <summary>
/// Encoding helpers for NIST P-256 (secp256r1) keys, using representations that
/// interoperate byte-for-byte with the Flutter client:
///   - private key: raw 32-byte big-endian scalar <c>d</c>
///   - public key : uncompressed point, 65 bytes (0x04 || X[32] || Y[32])
/// </summary>
public static class P256
{
    public const int FieldBytes = 32;
    public const int UncompressedLength = 65;

    public static ECParameters PrivateParams(byte[] d, byte[] uncompressedPublic)
    {
        var (x, y) = SplitPoint(uncompressedPublic);
        return new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = LeftPad(d, FieldBytes),
            Q = new ECPoint { X = x, Y = y },
        };
    }

    public static ECParameters PublicParams(byte[] uncompressedPublic)
    {
        var (x, y) = SplitPoint(uncompressedPublic);
        return new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = x, Y = y },
        };
    }

    public static byte[] Uncompressed(ECParameters p)
    {
        if (p.Q.X is null || p.Q.Y is null)
        {
            throw new ArgumentException("EC parameters have no public point.");
        }
        var buffer = new byte[UncompressedLength];
        buffer[0] = 0x04;
        LeftPad(p.Q.X, FieldBytes).CopyTo(buffer, 1);
        LeftPad(p.Q.Y, FieldBytes).CopyTo(buffer, 1 + FieldBytes);
        return buffer;
    }

    private static (byte[] X, byte[] Y) SplitPoint(byte[] uncompressed)
    {
        if (uncompressed.Length != UncompressedLength || uncompressed[0] != 0x04)
        {
            throw new ArgumentException("Expected a 65-byte uncompressed P-256 point.");
        }
        var x = uncompressed[1..(1 + FieldBytes)];
        var y = uncompressed[(1 + FieldBytes)..];
        return (x, y);
    }

    private static byte[] LeftPad(byte[] value, int length)
    {
        if (value.Length == length) return value;
        if (value.Length > length)
        {
            // Trim a leading zero byte (e.g. from a sign-extended big-endian scalar).
            var trimmed = value[(value.Length - length)..];
            return trimmed;
        }
        var padded = new byte[length];
        value.CopyTo(padded, length - value.Length);
        return padded;
    }
}
