using CtxApp.Api.Localization;
using CtxApp.Api.Security;

namespace CtxApp.Api.Configuration;

/// <summary>
/// Middleware section of the composition root. <c>Program.cs</c> calls
/// <see cref="UseCtxPipeline"/> after the host is built to assemble the request
/// pipeline. Order matters: the culture is resolved from <c>Accept-Language</c>
/// before anything renders text, then the security plane runs.
/// </summary>
public static class RequestPipeline
{
    public static WebApplication UseCtxPipeline(this WebApplication app)
    {
        // Resolve the request culture from Accept-Language before anything renders text.
        app.UseCtxLocalization();

        app.UseCtxSecurity();

        return app;
    }
}
