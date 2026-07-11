namespace Domain.Exceptions;

/// <summary>
/// The only exception type whose message is broadcast to clients
/// (FILTERS_AND_MIDDLEWARE.md — GlobalExceptionHandler disclosure policy).
/// StatusCode drives the ProblemDetails status.
/// </summary>
public class DomainException(string message, int statusCode = DomainException.StatusCodes.BadRequest)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static class StatusCodes
    {
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int Conflict = 409;
    }

    public static DomainException NotFound(string message) => new(message, StatusCodes.NotFound);
    public static DomainException Conflict(string message) => new(message, StatusCodes.Conflict);
    public static DomainException Unauthorized(string message) => new(message, StatusCodes.Unauthorized);
    public static DomainException Forbidden(string message) => new(message, StatusCodes.Forbidden);
}
