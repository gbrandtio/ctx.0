using System.Security.Cryptography;
using System.Text;
using Acme.Application.Abstractions;
using Acme.Infrastructure.Security.Crypto;

namespace Acme.Infrastructure.Security.Envelope;

/// <summary>
/// Envelope encryption with a per-value data key (DEK) wrapped by a versioned
/// key-encryption key (KEK), all AES-256-GCM. The encoded form is
/// <c>kekVersion.wrapIv.wrappedDek.dataIv.dataCipher</c> (each part base64), so
/// the KEK version travels with the value and keys can be rotated.
/// </summary>
public sealed class EnvelopeFieldCipher : IFieldCipher
{
    private readonly IReadOnlyDictionary<string, byte[]> _keks;
    private readonly string _activeVersion;
    private readonly byte[] _activeKek;

    public EnvelopeFieldCipher(EnvelopeOptions options)
    {
        if (options.Keks.Count == 0)
        {
            throw new InvalidOperationException("Ctx:Envelope:Keks is not configured. Run `ctx0 keygen`.");
        }
        _keks = options.Keks.ToDictionary(kv => kv.Key, kv => DecodeKek(kv.Key, kv.Value));
        _activeVersion = options.ActiveKekVersion;
        if (!_keks.TryGetValue(_activeVersion, out var active))
        {
            throw new InvalidOperationException($"Ctx:Envelope:ActiveKekVersion '{_activeVersion}' has no configured KEK.");
        }
        _activeKek = active;
    }

    public string Encrypt(string plaintext)
    {
        var dek = RandomNumberGenerator.GetBytes(32);
        var dataIv = RandomNumberGenerator.GetBytes(12);
        var (dataCt, dataTag) = AleCipher.Encrypt(dek, dataIv, Encoding.UTF8.GetBytes(plaintext));

        var wrapIv = RandomNumberGenerator.GetBytes(12);
        var (wrapCt, wrapTag) = AleCipher.Encrypt(_activeKek, wrapIv, dek);

        return string.Join('.',
            _activeVersion,
            Convert.ToBase64String(wrapIv),
            Convert.ToBase64String(Concat(wrapCt, wrapTag)),
            Convert.ToBase64String(dataIv),
            Convert.ToBase64String(Concat(dataCt, dataTag)));
    }

    public string Decrypt(string envelope)
    {
        var parts = envelope.Split('.');
        if (parts.Length != 5)
        {
            throw new CryptographicException("Malformed encryption envelope.");
        }
        if (!_keks.TryGetValue(parts[0], out var kek))
        {
            throw new CryptographicException($"Unknown KEK version '{parts[0]}'.");
        }

        var wrapIv = Convert.FromBase64String(parts[1]);
        var (wrapCt, wrapTag) = SplitTag(Convert.FromBase64String(parts[2]));
        var dek = AleCipher.Decrypt(kek, wrapIv, wrapCt, wrapTag);

        var dataIv = Convert.FromBase64String(parts[3]);
        var (dataCt, dataTag) = SplitTag(Convert.FromBase64String(parts[4]));
        return Encoding.UTF8.GetString(AleCipher.Decrypt(dek, dataIv, dataCt, dataTag));
    }

    private static byte[] DecodeKek(string version, string base64)
    {
        var key = Convert.FromBase64String(base64);
        if (key.Length != 32)
        {
            throw new InvalidOperationException($"KEK '{version}' must be 32 bytes (base64).");
        }
        return key;
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var result = new byte[a.Length + b.Length];
        a.CopyTo(result, 0);
        b.CopyTo(result, a.Length);
        return result;
    }

    private static (byte[] Body, byte[] Tag) SplitTag(byte[] bytes)
    {
        const int tagLength = 16;
        return (bytes[..^tagLength], bytes[^tagLength..]);
    }
}
