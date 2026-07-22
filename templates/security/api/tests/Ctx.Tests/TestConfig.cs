using System.Security.Cryptography;
using CtxApp.Infrastructure.Security.Crypto;
using Microsoft.AspNetCore.Hosting;

namespace CtxApp.Tests;

/// <summary>Applies a full set of valid security secrets to a test web host.</summary>
public static class TestConfig
{
    public static void Apply(IWebHostBuilder builder)
    {
        using var ale = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ale.ExportParameters(true);
        builder.UseSetting("CTX_ALE_PRIVATE_KEY", Convert.ToBase64String(p.D!));
        builder.UseSetting("CTX_ALE_PUBLIC_KEY", Convert.ToBase64String(P256.Uncompressed(p)));
        builder.UseSetting("CTX_JWT_SIGNING_KEY", "test-signing-key-that-is-long-enough-0123456789");
        builder.UseSetting("CTX_ENVELOPE_KEKS_1", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("CTX_ENVELOPE_ACTIVE_KEK_VERSION", "1");
        builder.UseSetting("CTX_ENVELOPE_BLIND_INDEX_KEY", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }
}
