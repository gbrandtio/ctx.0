# Logging Abstraction

The `ctx.0` mobile scaffold uses a centralized logging abstraction to handle application logs, diagnostics, and crash reporting. This ensures that debug prints do not leak into production builds and that error reporting is seamlessly integrated with remote monitoring services.

## The `LoggingService`

All logging within the application should be routed through the `LoggingService` interface (`lib/core/utils/logging_service.dart`), which provides two primary methods:

- `info(String message)`: Used for general informational messages and debug printing.
- `error(String message, [Object? error, StackTrace? stackTrace])`: Used for logging exceptions, errors, and critical failures.

## Implementation

The default implementation provided in the scaffold is the `ConsoleLoggingService`.

- **Debug Mode (`kDebugMode`)**:
  - `info()` delegates to `debugPrint()`.
  - `error()` delegates to `developer.log()` with a severity level of `1000` (`Level.SEVERE`), including the exception and stack trace.
- **Release Mode**:
  - `info()` is a no-op, preventing debug information from leaking in production.
  - `error()` is designed to forward critical exceptions to remote crash reporting tools (e.g., Firebase Crashlytics, Sentry) by adding the corresponding implementation inside the `error` method override.

## Usage

You should never use `print()` or `debugPrint()` directly in the application code. Instead, inject or locate the `LoggingService` and use it:

```dart
// Example Usage
try {
  // Application logic
  loggingService.info('Operation started');
} catch (e, stackTrace) {
  loggingService.error('Operation failed', e, stackTrace);
}
```

This abstraction allows you to easily swap the underlying logging implementation without changing the application logic, simply by providing a different `LoggingService` implementation (e.g., `CrashlyticsLoggingService`) in the dependency injection container.
