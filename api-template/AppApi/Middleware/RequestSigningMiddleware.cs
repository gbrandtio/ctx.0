using Application.Abstractions;
using Infrastructure.Security;
using Microsoft.Extensions.Options;
using SharedKernel.Clock;

namespace AppApi.Middleware;

/// <summary>
/// ECDSA P-256 request-signature verification
/// (APPLICATION_LAYER_SECURITY.md §2). Runs AFTER AleMiddleware so the
/// canonical string METHOD|PATH|TIMESTAMP|BODY is built from the
/// decrypted plaintext. Timestamps outside the signature window are
/// rejected (replay protection). Registration/metadata endpoints bypass
/// via [SkipRequestSigning] plus a path-based fail-safe.
/// </summary>
public sealed class RequestSigningMiddleware(
    RequestDelegate next,
    IOptions<AleOptions> options,
    IClock clock)
{
    public const string DeviceIdHeader = "X-App-Device-Id";
    public const string SignatureHeader = "X-App-Signature";

    private static readonly string[] BypassPaths =
    [
        "/v1/security/app-instances",
        "/v1/security/metadata",
    ];

    public async Task Invoke(HttpContext context, IAppInstanceRepository appInstances)
    {
        if (ShouldBypass(context))
        {
            await next(context);
            return;
        }

        var signatureHeader = context.Request.Headers[SignatureHeader].FirstOrDefault();
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

        var separator = signatureHeader.IndexOf(':');
        if (separator <= 0 ||
            !long.TryParse(signatureHeader[..separator], out var timestamp))
        {
            await Reject(context, "Request signature verification failed.");
            return;
        }

        var now = new DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds();
        if (Math.Abs(now - timestamp) > options.Value.SignatureWindowSeconds)
        {
            await Reject(context, "Request signature verification failed.");
            return;
        }

        var deviceId = context.Request.Headers[DeviceIdHeader].FirstOrDefault();
        if (string.IsNullOrEmpty(deviceId))
        {
            await Reject(context, "Request signature is required.");
            return;
        }

        var instance = await appInstances.FindByDeviceIdAsync(
            deviceId, context.RequestAborted);
        if (instance is null)
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

        var canonical =
            $"{context.Request.Method.ToUpperInvariant()}" +
            $"|{context.Request.Path.Value!.ToLowerInvariant()}" +
            $"|{timestamp}" +
            $"|{body.Trim()}";

        if (!EcdsaSignatureVerifier.Verify(
                instance.PublicKey, canonical, signatureHeader[(separator + 1)..]))
        {
            await Reject(context, "Request signature verification failed.");
            return;
        }

        await next(context);
    }

    private static bool ShouldBypass(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<SkipRequestSigningAttribute>() is not null ||
        BypassPaths.Any(p => context.Request.Path.StartsWithSegments(p));

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
