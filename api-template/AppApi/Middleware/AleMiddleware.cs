using System.Text;
using Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace AppApi.Middleware;

/// <summary>
/// Application-Layer Encryption (APPLICATION_LAYER_SECURITY.md §1): runs
/// FIRST — unwraps the per-request session key (RSA-OAEP-SHA256),
/// decrypts the request body, and encrypts 2xx responses with the same
/// key. Enforced in Production/Staging; optional in Development.
/// Endpoints marked [AllowPlaintext] (SSE, webhooks) bypass entirely.
/// </summary>
public sealed class AleMiddleware(
    RequestDelegate next,
    AleCryptoService crypto,
    IOptions<AleOptions> options)
{
    public const string EnabledHeader = "X-ALE-Enabled";
    public const string SessionKeyHeader = "X-ALE-Session-Key";
    private const string SessionKeyItem = "ale:session-key";

    public async Task Invoke(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<AllowPlaintextAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var wrappedKey = context.Request.Headers[SessionKeyHeader].FirstOrDefault();
        if (wrappedKey is null)
        {
            if (options.Value.Enforced)
            {
                await Reject(context, "ALE is required for this endpoint.");
                return;
            }
            await next(context); // Development: plaintext allowed
            return;
        }

        byte[] sessionKey;
        try
        {
            sessionKey = crypto.UnwrapSessionKey(wrappedKey);
        }
        catch (Exception)
        {
            await Reject(context, "Invalid ALE session key.");
            return;
        }

        try
        {
            context.Items[SessionKeyItem] = sessionKey;

            if (context.Request.ContentLength is > 0)
            {
                using var reader = new StreamReader(context.Request.Body);
                var base64Body = (await reader.ReadToEndAsync()).Trim().Trim('"');
                var plaintext = AleCryptoService.Decrypt(sessionKey, base64Body);
                context.Request.Body = new MemoryStream(plaintext);
                context.Request.ContentLength = plaintext.Length;
                context.Request.ContentType = "application/json";
            }

            // Buffer the response so a 2xx body can be encrypted after the
            // endpoint runs. Error responses are NEVER encrypted.
            var originalBody = context.Response.Body;
            using var buffer = new MemoryStream();
            context.Response.Body = buffer;
            try
            {
                await next(context);

                buffer.Position = 0;
                if (context.Response.StatusCode is >= 200 and < 300 && buffer.Length > 0)
                {
                    var ciphertext = AleCryptoService.Encrypt(sessionKey, buffer.ToArray());
                    var payload = Encoding.UTF8.GetBytes(ciphertext);
                    context.Response.Headers[EnabledHeader] = "true";
                    context.Response.ContentLength = payload.Length;
                    context.Response.ContentType = "text/plain";
                    await originalBody.WriteAsync(payload);
                }
                else
                {
                    await buffer.CopyToAsync(originalBody);
                }
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }
        finally
        {
            // Zero-memory hygiene: the session key dies with the exchange.
            Array.Clear(sessionKey);
            context.Items.Remove(SessionKeyItem);
        }
    }

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
