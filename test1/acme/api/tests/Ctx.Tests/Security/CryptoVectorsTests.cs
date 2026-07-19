using System.Text;
using Acme.Infrastructure.Security.Crypto;
using Xunit;

namespace Acme.Tests.Security;

/// <summary>
/// Asserts the API crypto reproduces the shared golden vectors byte-for-byte, so
/// it stays wire-compatible with the Flutter client.
/// </summary>
public class CryptoVectorsTests
{
    private static readonly System.Text.Json.JsonElement Vectors = GoldenVectors.Load();

    [Fact]
    public void Ecdh_agreement_matches_from_both_sides()
    {
        var ale = Vectors.GetProperty("ale");
        var serverPub = ale.B64("serverPublicB64");
        var ephPub = ale.B64("ephemeralPublicB64");

        var k1 = AleCipher.DeriveKey(P256.PrivateParams(ale.B64("ephemeralPrivateB64"), ephPub), serverPub);
        var k2 = AleCipher.DeriveKey(P256.PrivateParams(ale.B64("serverPrivateB64"), serverPub), ephPub);

        Assert.Equal(k1, k2);
        Assert.Equal(ale.Str("derivedKeyB64"), Convert.ToBase64String(k1));
    }

    [Fact]
    public void Encrypt_reproduces_golden_ciphertext_and_tag()
    {
        var ale = Vectors.GetProperty("ale");
        var (ct, tag) = AleCipher.Encrypt(
            ale.B64("derivedKeyB64"),
            ale.B64("ivB64"),
            Encoding.UTF8.GetBytes(ale.Str("plaintextUtf8")));

        Assert.Equal(ale.Str("ciphertextB64"), Convert.ToBase64String(ct));
        Assert.Equal(ale.Str("tagB64"), Convert.ToBase64String(tag));
    }

    [Fact]
    public void Decrypt_recovers_plaintext()
    {
        var ale = Vectors.GetProperty("ale");
        var plaintext = AleCipher.Decrypt(
            ale.B64("derivedKeyB64"), ale.B64("ivB64"), ale.B64("ciphertextB64"), ale.B64("tagB64"));

        Assert.Equal(ale.Str("plaintextUtf8"), Encoding.UTF8.GetString(plaintext));
    }

    [Fact]
    public void Canonical_string_matches()
    {
        var s = Vectors.GetProperty("signing");
        var body = Encoding.UTF8.GetBytes(s.Str("bodyUtf8"));
        var canonical = RequestSignature.Canonical(s.Str("method"), s.Str("path"), s.Str("timestamp"), body);

        Assert.Equal(s.Str("canonicalString"), canonical);
    }

    [Fact]
    public void Verifies_golden_signature()
    {
        var s = Vectors.GetProperty("signing");
        var body = Encoding.UTF8.GetBytes(s.Str("bodyUtf8"));

        Assert.True(RequestSignature.Verify(
            s.B64("devicePublicB64"), s.Str("signatureB64"),
            s.Str("method"), s.Str("path"), s.Str("timestamp"), body));
    }
}
