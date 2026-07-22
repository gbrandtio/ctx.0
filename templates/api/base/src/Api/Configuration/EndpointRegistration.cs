using CtxApp.Api.Security;

namespace CtxApp.Api.Configuration;

/// <summary>
/// Endpoint-mapping section of the composition root. <c>Program.cs</c> calls
/// <see cref="MapCtxEndpoints"/> to map the base routes and the always-on security
/// endpoints; enabled features map their routes at the <c>endpoints</c> anchor below.
/// </summary>
public static class EndpointRegistration
{
    public static WebApplication MapCtxEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // Always-on security endpoints: ALE key discovery + device key enrollment.
        app.MapCtxSecurityEndpoints();

        // ctx:anchor:endpoints

        return app;
    }
}
