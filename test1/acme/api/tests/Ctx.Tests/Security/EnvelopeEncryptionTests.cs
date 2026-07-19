using System.Security.Cryptography;
using Acme.Infrastructure.Security.Envelope;
using Xunit;

namespace Acme.Tests.Security;

/// <summary>Unit tests for envelope encryption and blind indexes (no database).</summary>
public class EnvelopeEncryptionTests
{
    private static EnvelopeOptions Options() => new()
    {
        Keks = { ["1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
        ActiveKekVersion = "1",
        BlindIndexKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    };

    [Fact]
    public void Round_trips_a_value()
    {
        var cipher = new EnvelopeFieldCipher(Options());
        Assert.Equal("social-security-number", cipher.Decrypt(cipher.Encrypt("social-security-number")));
    }

    [Fact]
    public void Ciphertext_is_non_deterministic_and_hides_plaintext()
    {
        var cipher = new EnvelopeFieldCipher(Options());
        var a = cipher.Encrypt("secret");
        var b = cipher.Encrypt("secret");
        Assert.NotEqual(a, b); // fresh DEK + IV each time
        Assert.DoesNotContain("secret", a);
    }

    [Fact]
    public void Tampering_is_detected()
    {
        var cipher = new EnvelopeFieldCipher(Options());
        var envelope = cipher.Encrypt("secret");
        var tampered = envelope[..^2] + (envelope[^1] == 'A' ? "BB" : "AA");
        Assert.ThrowsAny<CryptographicException>(() => cipher.Decrypt(tampered));
    }

    [Fact]
    public void Encodes_the_kek_version_so_keys_can_rotate()
    {
        var options = Options();
        var envelope = new EnvelopeFieldCipher(options).Encrypt("secret");
        Assert.StartsWith("1.", envelope);

        // A cipher that still holds version 1 can decrypt even after a new active version is added.
        options.Keks["2"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var rotated = new EnvelopeOptions { Keks = options.Keks, ActiveKekVersion = "2", BlindIndexKey = options.BlindIndexKey };
        Assert.Equal("secret", new EnvelopeFieldCipher(rotated).Decrypt(envelope));
    }

    [Fact]
    public void Blind_index_is_deterministic_and_normalized()
    {
        var index = new HmacBlindIndex(Options());
        Assert.Equal(index.Compute("Alice@Example.com"), index.Compute("  alice@example.com "));
        Assert.NotEqual(index.Compute("alice@example.com"), index.Compute("bob@example.com"));
    }
}
