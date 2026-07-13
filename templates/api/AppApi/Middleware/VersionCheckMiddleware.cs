using Microsoft.AspNetCore.Mvc;

namespace AppApi.Middleware;

/// <summary>
/// Intercepts requests to enforce a minimum client version.
/// If the client version is missing or below the configured minimum,
/// it responds with a 426 Upgrade Required.
/// </summary>
public sealed class VersionCheckMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<VersionCheckMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var minVersionString = configuration["ClientSettings:MinimumClientVersion"];
        if (!string.IsNullOrEmpty(minVersionString) && Version.TryParse(minVersionString, out var minVersion))
        {
            var clientVersionString = context.Request.Headers["X-Client-Version"].FirstOrDefault();
            
            // Note: If version is totally missing, we could either allow it or block it.
            // The requirement asks to force out of date apps. It's safer to block missing headers
            // as it means it's an old app that didn't have the interceptor.
            if (string.IsNullOrEmpty(clientVersionString) || 
                !Version.TryParse(clientVersionString, out var clientVersion) || 
                clientVersion < minVersion)
            {
                logger.LogWarning("Client version {ClientVersion} is below minimum {MinVersion}", 
                    clientVersionString ?? "unknown", minVersionString);

                context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status426UpgradeRequired,
                    Title = "Upgrade Required",
                    Detail = "Please update your app to the latest version to continue.",
                    Instance = context.Request.Path,
                    Extensions = 
                    { 
                        ["minimumRequiredVersion"] = minVersionString,
                        ["traceId"] = context.TraceIdentifier
                    }
                });
                return;
            }
        }

        await next(context);
    }
}
