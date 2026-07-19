using System.Text.Json;
using Acme.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Acme.Api.Endpoints;

/// <summary>
/// A secure echo endpoint. It requires a signed, ALE-encrypted request (enforced
/// by <see cref="CtxSecureEndpointFilter"/>) and returns an ALE-sealed reply,
/// demonstrating the full ctx.0 wire protocol round trip.
/// </summary>
public static class PingEndpoints
{
    public static IEndpointRouteBuilder MapPingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/ping", (HttpContext http) =>
            {
                var session = (AleSession)http.Items[AleSession.ItemKey]!;
                var request = JsonSerializer.Deserialize<JsonElement>(session.RequestPlaintext);
                var message = request.TryGetProperty("message", out var m) ? m.GetString() : null;
                return AleResults.Sealed(http, new { pong = true, echo = message });
            })
            .AddEndpointFilter<CtxSecureEndpointFilter>();

        return app;
    }
}
