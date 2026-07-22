using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CtxApp.Infrastructure.Security.Crypto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace CtxApp.Tests;

/// <summary>
/// Drives the full ctx.0 wire protocol against a live in-process API: enroll a
/// device key, fetch the server ALE key, then send a signed + ALE-encrypted
/// request and open the ALE-sealed reply.
/// </summary>
public class PingRoundTripTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PingRoundTripTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            // The signed/ALE ping path needs no database; Production keeps the
            // RLS startup initializer from touching one.
            builder.UseEnvironment("Production");
            TestConfig.Apply(builder);
            builder.UseSetting("ConnectionStrings:Default", "Host=localhost;Database=ctxapp;Username=ctxapp;Password=x");
        });
    }

    [Fact]
    public async Task Signed_and_encrypted_ping_round_trips()
    {
        var client = _factory.CreateClient();

        // Device key pair.
        using var device = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var dp = device.ExportParameters(true);
        var devicePublic = P256.Uncompressed(dp);
        const string deviceId = "test-device-1";

        // 1. Fetch the server ALE public key.
        var aleKeyDoc = await client.GetFromJsonAsync<JsonElement>("/v1/security/ale-public-key");
        var serverAlePublic = Convert.FromBase64String(aleKeyDoc.GetProperty("publicKey").GetString()!);

        // 2. Enroll the device signing key.
        var enroll = await client.PostAsJsonAsync("/v1/security/devices",
            new { deviceId, publicKey = Convert.ToBase64String(devicePublic) });
        Assert.Equal(HttpStatusCode.NoContent, enroll.StatusCode);

        // 3. Seal a request body with ALE (client ephemeral key -> server static key).
        using var ephemeral = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var ep = ephemeral.ExportParameters(true);
        var ephPublic = P256.Uncompressed(ep);
        var iv = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes("{\"message\":\"marco\"}");
        var key = AleCipher.DeriveKey(P256.PrivateParams(ep.D!, ephPublic), serverAlePublic);
        var (cancellationToken, tag) = AleCipher.Encrypt(key, iv, plaintext);
        var envelope = new AleEnvelope(
            Convert.ToBase64String(ephPublic),
            Convert.ToBase64String(iv),
            Convert.ToBase64String(cancellationToken),
            Convert.ToBase64String(tag));
        var body = JsonSerializer.SerializeToUtf8Bytes(envelope);

        // 4. Sign the canonical request over the exact body bytes.
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var signature = RequestSignature.Sign(P256.PrivateParams(dp.D!, devicePublic), "POST", "/v1/ping", timestamp, body);

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/ping")
        {
            Content = new ByteArrayContent(body),
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add(CtxProtocol.ProtocolHeader, CtxProtocol.Version);
        request.Headers.Add(CtxProtocol.DeviceIdHeader, deviceId);
        request.Headers.Add(CtxProtocol.TimestampHeader, timestamp);
        request.Headers.Add(CtxProtocol.SignatureHeader, signature);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // 5. Open the ALE-sealed reply with the key derived when sealing the request.
        var sealed_ = await response.Content.ReadFromJsonAsync<AleEnvelope>();
        var replyBytes = AleCipher.Decrypt(
            key,
            Convert.FromBase64String(sealed_!.Iv),
            Convert.FromBase64String(sealed_.Ct),
            Convert.FromBase64String(sealed_.Tag));
        var reply = JsonSerializer.Deserialize<JsonElement>(replyBytes);

        Assert.True(reply.GetProperty("pong").GetBoolean());
        Assert.Equal("marco", reply.GetProperty("echo").GetString());
    }

    [Fact]
    public async Task Ping_without_signature_is_rejected()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/v1/ping", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
