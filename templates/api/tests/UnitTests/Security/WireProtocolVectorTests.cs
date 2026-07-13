using System.Text;
using System.Text.Json;

namespace UnitTests.Security;

/// <summary>
/// Golden wire-protocol vectors shared with the mobile package's test
/// suite (packages/protocol/wire_protocol_vectors.json). If this test
/// fails after a change, the mobile and server security planes no longer
/// speak the same protocol — bump CtxProtocol on BOTH sides and
/// regenerate the vectors deliberately.
/// </summary>
public class WireProtocolVectorTests
{
    private static readonly JsonElement Vectors = LoadVectors();

    private static JsonElement LoadVectors()
    {
        // The vectors ship with the repo (.ctx/) — the wire-protocol
        // contract travels with the generated project; in the ctx.0
        // monorepo the canonical copy lives at packages/protocol/.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
             dir is not null;
             dir = dir.Parent)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(dir.FullName, ".ctx", "wire_protocol_vectors.json"),
                Path.Combine(dir.FullName, "packages", "protocol",
                    "wire_protocol_vectors.json"),
            })
            {
                if (File.Exists(candidate))
                {
                    return JsonDocument.Parse(File.ReadAllText(candidate)).RootElement;
                }
            }
        }
        throw new FileNotFoundException("wire_protocol_vectors.json not found");
    }

    [Fact]
    public void Package_protocol_version_matches_the_shared_vectors() =>
        Assert.Equal(Vectors.GetProperty("protocolVersion").GetString(),
            CtxProtocol.Version);

    [Fact]
    public void Canonical_signing_string_matches_the_middleware_construction()
    {
        var signing = Vectors.GetProperty("signing");
        // Mirrors RequestSigningMiddleware: METHOD|lowercase path|timestamp|body.
        var canonical =
            $"{signing.GetProperty("method").GetString()!.ToUpperInvariant()}" +
            $"|{signing.GetProperty("path").GetString()!.ToLowerInvariant()}" +
            $"|{signing.GetProperty("timestamp").GetInt64()}" +
            $"|{signing.GetProperty("body").GetString()}";
        Assert.Equal(signing.GetProperty("canonical").GetString(), canonical);
    }

    [Fact]
    public void AesGcm_payload_decrypts_like_the_mobile_client()
    {
        var aes = Vectors.GetProperty("aesGcm");
        var key = Convert.FromBase64String(aes.GetProperty("keyBase64").GetString()!);
        var payload = Convert.FromBase64String(aes.GetProperty("payloadBase64").GetString()!);
        var plaintext = AesEncryptionProvider.DecryptBytes(key, payload);
        Assert.Equal(aes.GetProperty("plaintext").GetString(),
            Encoding.UTF8.GetString(plaintext));
    }
}
