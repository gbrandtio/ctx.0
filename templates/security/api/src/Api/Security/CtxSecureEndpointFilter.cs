using System.Text.Json;
using CtxApp.Infrastructure.Security;
using CtxApp.Infrastructure.Security.Crypto;
using Microsoft.AspNetCore.Http;

namespace CtxApp.Api.Security;

/// <summary>
/// Enforces the ctx.0 wire protocol on the endpoints it is attached to: protocol
/// version, timestamp freshness, ECDSA request-signature verification against the
/// enrolled device key, and ALE decryption. On success the decrypted body and
/// the derived key are exposed via <see cref="AleSession"/> in
/// <c>HttpContext.Items</c>; any failure short-circuits with 401.
/// </summary>
public sealed class CtxSecureEndpointFilter(IDeviceKeyRegistry devices, IAleKeyProvider aleKeys) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var request = http.Request;

        if (request.Headers[CtxProtocol.ProtocolHeader] != CtxProtocol.Version)
        {
            return Unauthorized("Unsupported or missing protocol version.");
        }

        var deviceId = request.Headers[CtxProtocol.DeviceIdHeader].ToString();
        var timestamp = request.Headers[CtxProtocol.TimestampHeader].ToString();
        var signature = request.Headers[CtxProtocol.SignatureHeader].ToString();
        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
        {
            return Unauthorized("Missing signing headers.");
        }

        if (!RequestSignature.TimestampFresh(timestamp, DateTimeOffset.UtcNow))
        {
            return Unauthorized("Request timestamp outside the accepted window.");
        }

        var devicePublic = devices.PublicKey(deviceId);
        if (devicePublic is null)
        {
            return Unauthorized("Unknown device.");
        }

        request.EnableBuffering();
        byte[] body;
        using (var ms = new MemoryStream())
        {
            await request.Body.CopyToAsync(ms);
            body = ms.ToArray();
        }
        request.Body.Position = 0;

        var pathAndQuery = request.Path + request.QueryString;
        if (!RequestSignature.Verify(devicePublic, signature, request.Method, pathAndQuery, timestamp, body))
        {
            return Unauthorized("Invalid request signature.");
        }

        AleEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<AleEnvelope>(body);
        }
        catch (JsonException)
        {
            return Unauthorized("Malformed ALE envelope.");
        }
        if (envelope?.Epk is null)
        {
            return Unauthorized("Missing ALE envelope.");
        }

        byte[] key;
        byte[] plaintext;
        try
        {
            key = AleCipher.DeriveKey(aleKeys.PrivateParameters, Convert.FromBase64String(envelope.Epk));
            plaintext = AleCipher.Decrypt(
                key,
                Convert.FromBase64String(envelope.Iv),
                Convert.FromBase64String(envelope.Ct),
                Convert.FromBase64String(envelope.Tag));
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or FormatException)
        {
            return Unauthorized("ALE decryption failed.");
        }

        http.Items[AleSession.ItemKey] = new AleSession(key, plaintext);
        return await next(context);
    }

    private static IResult Unauthorized(string reason) => Results.Json(new { error = reason }, statusCode: StatusCodes.Status401Unauthorized);
}
