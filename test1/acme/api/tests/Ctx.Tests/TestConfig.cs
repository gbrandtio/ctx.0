using System.Security.Cryptography;
using Acme.Infrastructure.Security.Crypto;
using Microsoft.AspNetCore.Hosting;

namespace Acme.Tests;

/// <summary>Applies a full set of valid security secrets to a test web host.</summary>
public static class TestConfig
{
    public static void Apply(IWebHostBuilder builder)
    {
        using var ale = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = ale.ExportParameters(true);
        builder.UseSetting("Ctx:Ale:PrivateKey", Convert.ToBase64String(p.D!));
        builder.UseSetting("Ctx:Ale:PublicKey", Convert.ToBase64String(P256.Uncompressed(p)));
        builder.UseSetting("Ctx:Jwt:SigningKey", "test-signing-key-that-is-long-enough-0123456789");
        builder.UseSetting("Ctx:Envelope:Keks:1", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Ctx:Envelope:ActiveKekVersion", "1");
        builder.UseSetting("Ctx:Envelope:BlindIndexKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }
}
