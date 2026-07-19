using System.Text.Json;
using Acme.Infrastructure.Security.Crypto;
using Microsoft.AspNetCore.Http;

namespace Acme.Api.Security;

/// <summary>Helpers for returning ALE-sealed responses from secure endpoints.</summary>
public static class AleResults
{
    /// <summary>Seal <paramref name="payload"/> with the request's ALE session key.</summary>
    public static IResult Sealed(HttpContext http, object payload)
    {
        if (http.Items[AleSession.ItemKey] is not AleSession session)
        {
            throw new InvalidOperationException(
                "No ALE session on the request. Attach CtxSecureEndpointFilter to this endpoint.");
        }
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        var envelope = AleCipher.SealResponse(session.Key, plaintext);
        http.Response.Headers[CtxProtocol.ProtocolHeader] = CtxProtocol.Version;
        return Results.Json(envelope);
    }
}
