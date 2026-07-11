using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AppApi.Middleware;

/// <summary>
/// Centralized error handling with a strict disclosure policy
/// (FILTERS_AND_MIDDLEWARE.md §2, ERROR_HANDLING.md): only
/// DomainException messages reach the client; everything else becomes a
/// generic ProblemDetails with a traceId for log correlation.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        var (status, title, detail) = exception switch
        {
            DomainException domainEx =>
                (domainEx.StatusCode, TitleFor(domainEx.StatusCode), domainEx.Message),
            BadHttpRequestException =>
                (StatusCodes.Status400BadRequest, "Bad Request",
                 "The request was malformed or could not be bound."),
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "Unauthorized",
                 "Authentication is required."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Internal Server Error",
                 "An unexpected error occurred. Please try again later."),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception (traceId {TraceId}).", traceId);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Extensions = { ["traceId"] = traceId },
            },
            cancellationToken);
        return true;
    }

    private static string TitleFor(int status) => status switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        _ => "Error",
    };
}
