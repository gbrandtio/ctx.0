# Filters & Middleware

This document describes the custom filters and middleware used in the API to enforce security, ownership, and cross-cutting concerns.

---

## 1. Endpoint Filters

Endpoint filters are applied to minimal API endpoints or route groups to perform logic before or after the endpoint handler executes.

### SanitizationFilter

The `SanitizationFilter` is a global endpoint filter that sanitizes incoming string data to prevent Cross-Site Scripting (XSS) attacks.

#### Behavior
- **Recursive Sanitization**: Recursively inspects all public instance properties of incoming DTOs.
- **HTML Sanitization**: Uses `HtmlSanitizer` to strip malicious tags and attributes (like `<script>` or `onerror`) while preserving safe content.
- **Collection Support**: Sanitizes strings within `List<string>`, `string[]`, and `Dictionary<string, string>`.
- **Depth Limit**: To prevent stack overflows from circular references, it limits recursion to a depth of **10**.
- **Performance**: Skips primitives, value types, and common immutable types.

#### Usage
The filter is typically applied to a top-level route group to ensure all endpoints are protected:

```csharp
var v1 = app.MapGroup("/v1")
    .AddEndpointFilter<SanitizationFilter>();
```

#### Files
- `AppApi/Filters/SanitizationFilter.cs`

---

## 2. Middleware

Middleware components are part of the ASP.NET Core request pipeline and handle cross-cutting concerns for all requests.

### GlobalExceptionHandler
The `GlobalExceptionHandler` implements the modern .NET `IExceptionHandler` interface to provide centralized, secure error handling. It catches all unhandled exceptions and returns a standardized `ProblemDetails` response.

For complete details on error classifications, formats, and best practices, see the [Error Handling Guide](ERROR_HANDLING.md).

#### Security & Information Disclosure
To prevent security vulnerabilities and information leakage, the handler follows a strict disclosure policy:
- **Domain Exceptions**: Only exceptions of type `DomainException` have their messages broadcasted to the client. The status code dynamically matches `domainEx.StatusCode` and the title maps to the corresponding HTTP status (e.g. `NotFound` to "Not Found").
- **Binding & Malformed Payloads**: `BadHttpRequestException` is mapped to a `400 Bad Request` with a safe message, masking internal framework details.
- **Unauthorized Access**: `UnauthorizedAccessException` is automatically mapped to a `401 Unauthorized` status code with a safe, generic message.
- **Generic Errors**: All other exceptions (including system, database, and third-party errors) result in a generic `500 Internal Server Error` message to the client.
- **Logging**: The full exception details, including stack traces and inner exceptions, are logged internally for debugging purposes, associated with a unique `traceId` returned in the response.

#### Usage
The handler is registered in `Program.cs` and enabled via the `app.UseExceptionHandler()` middleware:

```csharp
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ... in the pipeline
app.UseExceptionHandler();
```

#### Files
- `AppApi/Middleware/GlobalExceptionHandler.cs`
- `Domain/Exceptions/DomainException.cs`

### RequestSigningMiddleware
Validates the ECDSA signature of incoming requests to ensure they originated from a trusted client and have not been tampered with. Runs **after** `AleMiddleware` so it verifies the signature against the decrypted plaintext body (see [Application-Layer Security](../security/APPLICATION_LAYER_SECURITY.md)).

### AleMiddleware (Application-Layer Encryption)
Handles the decryption of incoming request bodies and the encryption of outgoing response bodies using AES-256-GCM.

---

## 3. Best Practices

1. **Apply filters at the group level** whenever possible to ensure consistent enforcement across related endpoints.
2. **Explicitly mark anonymous endpoints** using `.AllowAnonymous()` when they are part of a group that has security filters applied.
3. **Never trust user IDs in request bodies**. Always prefer extracting the identity (UID) from the JWT claims.
4. **Enforce resource ownership** using the standardized authorization policies (e.g., `Policies.UserSelf`, `Policies.ProjectRead`) which verify route parameters against authenticated identity claims.
