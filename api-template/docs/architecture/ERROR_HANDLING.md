# Error Handling Architecture

This document describes the unified error handling architecture of the API, designed to provide consistent, user-friendly responses to the frontend while securing the internal architecture and stack traces from public exposure.

---

## 1. Unified Response Structure (`ProblemDetails`)

All API error responses conform to the standard **RFC 9457 (Problem Details for HTTP APIs)** format (the successor to RFC 7807). Under no circumstances should raw exception stack traces, framework serialization details, or database errors be returned to the client.

### Standard Error JSON Schema
```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "Email already exists.",
  "instance": "/v1/users",
  "traceId": "0HN18E6N3C4R1:00000001"
}
```

*   `status`: The HTTP Status Code (e.g., 400, 401, 403, 404, 409, 500).
*   `title`: A short, human-readable summary matching the status code (e.g., "Bad Request", "Conflict", "Internal Server Error").
*   `detail`: A human-readable, user-friendly description of the error.
*   `instance`: The URI path where the error occurred.
*   `traceId`: A unique request tracking ID correlated with the internal application logs.

---

## 2. Core Components

### GlobalExceptionHandler
Catches all unhandled exceptions occurring during HTTP requests and maps them to standard `ProblemDetails` format before serialization.
- **Location**: `AppApi/Middleware/GlobalExceptionHandler.cs`
- **Behavior**:
  - **Client-Safe Exceptions** (`DomainException`): The message inside a `DomainException` (or subclass) is considered safe for frontend consumption. The exception's defined `StatusCode` is mapped directly to the response status code, and its title is dynamically set (e.g., `404` maps to "Not Found").
  - **Malformed Request Payload** (`BadHttpRequestException`): Returns `400 Bad Request` with a generic, safe detail: `"The request payload is malformed or invalid."`
  - **Security Access Failures** (`UnauthorizedAccessException`): Returns `401 Unauthorized` with a generic detail: `"You do not have permission to perform this action."`
  - **Internal Server Errors** (`Exception` / Fallback): All other exceptions (EF Core, null-references, third-party services) are captured, logged internally with a full stack trace and the corresponding `traceId`, and returned to the client as `500 Internal Server Error` with a generic message: `"An unexpected error occurred. Please contact support."`

### Middleware Errors
Authentication and decryption components running prior to endpoint invocation also return standard `ProblemDetails` JSON to ensure consistency:
- **AleMiddleware** (`X-ALE-Session-Key` or body decryption failure): Returns `400 Bad Request`.
- **RequestSigningMiddleware** (`X-App-Signature` mismatch or missing headers): Returns `401 Unauthorized` (e.g., `"Device not registered"` or `"Request signature verification failed"`).

---

## 3. Best Practices for Developers

### Rule 1: Throw `DomainException` for Client-Safe Errors
Whenever input parameters fail validations inside Value Objects (e.g., `Email.Create`, `Money`) or command handlers fail unique checks (e.g., `CreateUserHandler`), always throw `DomainException` to safely propagate the validation error message to the client.

```csharp
// In Value Objects / Validators:
if (value <= 0)
{
    throw new DomainException("Money value must be greater than zero.", HttpStatusCode.BadRequest);
}

// In Command Handlers / Services:
if (emailAlreadyExists)
{
    throw new DomainException("Email already exists.", HttpStatusCode.Conflict);
}
```

### Rule 2: Never Let System Exceptions Escape with Custom Messages
Never throw raw `InvalidOperationException`, `ArgumentException`, or `NullReferenceException` with validation messages meant for the frontend. System exceptions are treated as unexpected server errors and will be stripped of their details for security.

---

## 4. Swagger OpenApi Integration

To ensure API clients can align with the expected error responses, the API registers a custom Swagger operation filter:
- **Location**: `AppApi/Filters/SwaggerErrorResponsesFilter.cs`
- **Behavior**:
  - **400 Bad Request** & **500 Internal Server Error** are automatically documented for every single endpoint.
  - **401 Unauthorized** & **403 Forbidden** are automatically documented for endpoints requiring authorization.
  - All errors are modeled with the `ProblemDetails` schema.
