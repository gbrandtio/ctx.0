using Ctx0.Security.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ctx0.Security;

/// <summary>
/// ECDSA P-256 request-signature verification
/// (APPLICATION_LAYER_SECURITY.md §2). Runs AFTER AleMiddleware so the
/// canonical string METHOD|PATH?QUERY|TIMESTAMP|NONCE|BODY is built from
/// the decrypted plaintext. Protocol 1.1: the query string is part of the
/// signature (a MITM can no longer tamper query parameters), and every
/// request carries a single-use nonce so a captured signed request cannot
/// be replayed within the timestamp window. Registration/metadata
/// endpoints bypass via [SkipRequestSigning] plus a path-based fail-safe.
/// </summary>
public sealed class RequestSigningMiddleware(
    RequestDelegate next,
    IOptions<AleOptions> options,
    IMemoryCache nonceCache,
    IClock clock)
{
    public async Task Invoke(HttpContext context, IDeviceKeyStore deviceKeys)
    {
        if (ShouldBypass(context))
        {
            await next(context);
            return;
        }

        var signatureHeader =
            context.Request.Headers[options.Value.SignatureHeader].FirstOrDefault();
        if (signatureHeader is null)
        {
            if (options.Value.RequestSigningRequired)
            {
                await Reject(context, "Request signature is required.");
                return;
            }
            await next(context); // Development: unsigned allowed
            return;
        }

        // Protocol 1.1 header: timestamp:nonce:signature (three parts).
        var parts = signatureHeader.Split(':');
        if (parts.Length != 3 ||
            !long.TryParse(parts[0], out var timestamp) ||
            parts[1].Length == 0)
        {
            await Reject(context, "Request signature verification failed.");
            return;
        }
        var nonce = parts[1];
        var signature = parts[2];

        var now = new DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > options.Value.SignatureWindowSeconds)
        {
            await Reject(context, "Request signature verification failed.");
            return;
        }

        var deviceId =
            context.Request.Headers[options.Value.DeviceIdHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(deviceId))
        {
            await Reject(context, "Request signature is required.");
            return;
        }

        var publicKey = await deviceKeys.FindPublicKeyAsync(
            deviceId, context.RequestAborted);
        if (publicKey is null)
        {
            await Reject(context, "Device not registered.");
            return;
        }

        // The body here is the decrypted plaintext (ALE ran first).
        context.Request.EnableBuffering();
        string body;
        using (var reader = new StreamReader(context.Request.Body, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Protocol 1.1: sign the path AND query string (QueryString.Value
        // already includes the leading '?'), the nonce, and the untrimmed
        // body — matching the mobile SecureDeviceSigningClient byte for byte.
        var pathAndQuery = context.Request.Path.Value!.ToLowerInvariant() +
            (context.Request.QueryString.HasValue
                ? context.Request.QueryString.Value
                : string.Empty);
        var canonical =
            $"{context.Request.Method.ToUpperInvariant()}" +
            $"|{pathAndQuery}" +
            $"|{timestamp}" +
            $"|{nonce}" +
            $"|{body}";

        if (!EcdsaSignatureVerifier.Verify(publicKey, canonical, signature))
        {
            await Reject(context, "Request signature verification failed.");
            return;
        }

        // Single-use nonce (per device) within the signature window: a
        // captured, still-in-window signed request cannot be replayed
        // because its nonce is already spent.
        var nonceKey = $"ctx-sig-nonce:{deviceId}:{nonce}";
        if (!nonceCache.TryGetValue(nonceKey, out _))
        {
            nonceCache.Set(nonceKey, true,
                TimeSpan.FromSeconds(options.Value.SignatureWindowSeconds));
        }
        else
        {
            await Reject(context, "Request signature verification failed.");
            return;
        }

        await next(context);
    }

    private bool ShouldBypass(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<SkipRequestSigningAttribute>() is not null ||
        options.Value.SigningBypassPaths.Any(
            p => context.Request.Path.StartsWithSegments(p));

    private static async Task Reject(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            status = 401,
            title = "Unauthorized",
            detail,
            instance = context.Request.Path.Value,
            traceId = context.TraceIdentifier,
        });
    }
}
