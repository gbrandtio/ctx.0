using Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace UnitTests.Security;

public sealed class CryptoTests
{
    private static AesEncryptionProvider Provider()
    {
        var key = Convert.ToBase64String(new byte[32]); // deterministic test KEK
        return new AesEncryptionProvider(Options.Create(new EncryptionOptions
        {
            CurrentVersion = "v1",
            Keys = new() { ["v1"] = new() { Key = key } },
        }));
    }

    [Fact]
    public void Envelope_round_trips_pii_through_a_wrapped_dek()
    {
        var provider = Provider();
        var dek = provider.GenerateDek();
        var wrapped = provider.WrapDek(dek);

        var ciphertext = provider.EncryptString(dek, "smoke@example.com");
        var unwrappedDek = provider.UnwrapDek(wrapped);

        Assert.StartsWith("v1:", wrapped);
        Assert.NotEqual("smoke@example.com", ciphertext);
        Assert.Equal("smoke@example.com", provider.DecryptString(unwrappedDek, ciphertext));
    }

    [Fact]
    public void Same_plaintext_encrypts_to_different_ciphertexts()
    {
        // Per-operation random nonces defeat frequency analysis.
        var provider = Provider();
        var dek = provider.GenerateDek();
        Assert.NotEqual(
            provider.EncryptString(dek, "same"),
            provider.EncryptString(dek, "same"));
    }

    [Fact]
    public void IsCurrentVersion_detects_stale_kek_prefix()
    {
        var provider = Provider();
        Assert.True(provider.IsCurrentVersion("v1:abc"));
        Assert.False(provider.IsCurrentVersion("v0:abc"));
    }

    [Fact]
    public void Ecdsa_verifier_accepts_a_valid_signature_and_rejects_tampering()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
        const string canonical = "POST|/v1/users|1700000000|{\"a\":1}";
        var signature = Convert.ToBase64String(ecdsa.SignData(
            System.Text.Encoding.UTF8.GetBytes(canonical),
            System.Security.Cryptography.HashAlgorithmName.SHA256));

        Assert.True(EcdsaSignatureVerifier.Verify(publicKey, canonical, signature));
        Assert.False(EcdsaSignatureVerifier.Verify(publicKey, canonical + "x", signature));
    }
}
