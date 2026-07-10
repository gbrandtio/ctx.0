# Error Handling Architecture — Mobile Client

This document describes the unified error handling architecture of the mobile client, designed to mirror and gracefully consume the robust error handling of the backend API.

---

## 1. RFC 9457 (Problem Details) Integration

The client uses the `ProblemDetails` model to parse all error responses (4xx and 5xx) from the API, following **RFC 9457** (Problem Details for HTTP APIs, which obsoletes RFC 7807). This ensures consistency and prevents technical details (like stack traces) from being exposed in the UI.

### ProblemDetails Model
Located in: `lib/core/models/problem_details.dart`

```dart
class ProblemDetails {
  final int? status;      // HTTP Status Code
  final String? title;   // Short summary (e.g., "Conflict", "Not Found")
  final String? detail;  // User-friendly description
  final String? instance; // Path where error occurred
  final String? traceId;  // Correlation ID for logs
}
```

---

## 2. Core Components

### `AppException`
A custom exception class that wraps `ProblemDetails`. It is the primary way errors are propagated through the application layers (Repository -> Bloc).
- **Location**: `lib/core/utils/app_exception.dart`
- **Key Methods**:
    - `isClientSafe`: Returns true for 4xx errors (excluding 401). These messages are considered safe to show to the user.
    - `userFriendlyMessage`: Provides a refined, safe message for the UI.

### `ApiBaseMixin`
The foundation for all API services. It contains the logic to intercept non-2xx responses and map them to `AppException`.
- **Location**: `lib/data/services/api/mixins/api_base_mixin.dart`
- **Behavior**:
    - If the response is JSON, it attempts to parse it into `ProblemDetails`.
    - If the response is not JSON (e.g., a gateway timeout or security layer failure), it generates a fallback "Internal Server Error" `ProblemDetails`.

---

## 3. Best Practices for Developers

### Rule 1: Always use `AppException.userFriendlyMessage` in the UI
Never display the raw `detail` or `title` fields directly unless you are sure they are safe. `userFriendlyMessage` handles the logic of when to show the server's message versus a generic fallback.

```dart
// In a Bloc: convert the failure into a state; the View shows it via BlocListener.
on<ProfileSaveRequested>((event, emit) async {
  emit(const ProfileState.loading());
  final result = await repository.updateProfile(event.profile);
  switch (result) {
    case Success():
      emit(ProfileState.success(result.value));
    case Failure(:final error):
      emit(ProfileState.failure(AppException.from(error).userFriendlyMessage));
  }
});
```

### Rule 2: Capture `traceId` for Support
Whenever an error is logged to an external service (like Sentry) or shown in a "Technical Details" section, ensure the `traceId` is included. This is the key for backend engineers to find the corresponding logs.

### Rule 3: Do Not Catch System Exceptions Globally
Let unexpected exceptions (like `TypeError`) propagate or handle them at the very top level (`BlocObserver.onError` logs uncaught Bloc errors). The `ApiBaseMixin` will wrap unexpected network/parsing errors into a generic 500 `AppException`.

---

## 4. UI Feedback Guidelines

- **401 Unauthorized**: Handled automatically by the `AuthRefreshClient` or by redirecting the user to the Login screen with a "Session expired" message.
- **403 Forbidden**: Show a message indicating the user doesn't have permission for that specific action.
- **400/404/409 (Client Safe)**: Show the `detail` provided by the API (e.g., "Email already exists.", "Item not found.").
- **500 Internal Server Error**: Always show a generic "Something went wrong. Please try again later." message.
