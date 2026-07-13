# State Management: Bloc Conventions

This project uses **Bloc** (`flutter_bloc`, `bloc`, `bloc_concurrency`) exclusively for state management. This document defines the mandatory conventions. For how Blocs fit into the overall layering, see `docs/FLUTTER_ARCHITECTURE.md`.

## 1. Cubit vs Bloc — Decision Rule

* **Cubit** (default): Use for straightforward state holders where the UI calls methods directly (theme, locale, settings, simple forms, toggles).
* **Bloc**: Use when you need **event transformers** (debounce, droppable, restartable), event traceability, or when multiple distinct triggers map to the same state machine (e.g., login screen: submit, Google sign-in, session-restore).

If in doubt, start with a Cubit; upgrade to a Bloc when you need a transformer.

## 2. Structure & Naming

* One Bloc/Cubit per feature screen (e.g., `LoginBloc`, `ProfileCubit`, `HomeBloc`). Global app-wide concerns get their own (e.g., `AuthBloc`, `ThemeCubit`, `LocaleCubit`).
* Files live in `lib/features/<feature>/bloc/`:
    * `<feature>_bloc.dart`, `<feature>_event.dart`, `<feature>_state.dart` (use `part`/`part of`).
* Events are named as past-tense facts or user intents: `LoginSubmitted`, `ProfileRefreshRequested`, `MapMarkerTapped`.

## 3. State Modeling

* States are **immutable** and **equatable**. Use `freezed` (preferred) or `equatable`.
* Model states as a **sealed class hierarchy** so `switch` handling is exhaustive:

```dart
@freezed
sealed class ProfileState with _$ProfileState {
  const factory ProfileState.initial() = ProfileInitial;
  const factory ProfileState.loading() = ProfileLoading;
  const factory ProfileState.success(UserProfile profile) = ProfileSuccess;
  const factory ProfileState.failure(String message) = ProfileFailure;
}
```

* For screens that keep data visible during refreshes, prefer a single state class with a `status` enum + `copyWith` instead of a sealed hierarchy:

```dart
enum HomeStatus { initial, loading, success, failure }
// HomeState(status, items, hasReachedMax, errorMessage)
```

* Never expose mutable collections in a state; copy lists (`List.unmodifiable` or freezed defaults).

## 4. Event Handling & Concurrency

* Register one `on<Event>` handler per event type. Handlers must be the only place that emits.
* Use `bloc_concurrency` transformers deliberately:
    * `droppable()`: submissions and refreshes that must not overlap (prevents double-tap double-submit).
    * `restartable()`: search-as-you-type, location updates (only the latest matters).
    * `sequential()`: ordered mutations (e.g., queueing offline writes).
* Convert Repository `Result` values to states with an explicit `switch`; never let exceptions escape a handler. Unexpected exceptions are caught by `BlocObserver.onError` for logging.

## 5. Widget Integration

* **`BlocProvider`** creates and disposes the Bloc; scope it to the feature route. Use `MultiBlocProvider` at the app root only for genuinely global Blocs (`AuthBloc`, `ThemeCubit`, `LocaleCubit`).
* **`RepositoryProvider`** exposes repositories; Blocs receive them via constructor (`context.read<UserRepository>()` at creation site).
* **`BlocBuilder`** for rendering state; always consider `buildWhen` or `BlocSelector` to narrow rebuilds.
* **`BlocListener`** for one-shot side effects: navigation, snackbars, dialogs, haptics. Never navigate or show snackbars inside `build()`/`BlocBuilder`.
* **`BlocConsumer`** only when the same widget needs both.
* `context.read<T>()` in callbacks; `context.watch<T>()` sparingly and only in `build()`.

## 6. Bloc-to-Bloc Communication

Blocs must not reference other Blocs directly. Choose one of:

1. **Shared Repository (preferred)**: both Blocs listen to the same repository stream (e.g., `AuthRepository.authStateChanges`). The repository is the SSOT.
2. **Stream subscription at the presentation edge**: a parent widget uses `BlocListener` on Bloc A and adds an event to Bloc B.

Example: on logout, `AuthBloc` calls `AuthRepository.logout()`; `ThemeCubit`, `LocaleCubit`, and `SettingsCubit` each listen to the repository's auth stream and reset themselves to defaults (prevents preference leakage to the next user, see `docs/SECURITY.md`).

## 7. Observability

* Register a global `BlocObserver` in `main.dart` that logs `onTransition` and `onError` in debug builds and forwards `onError` to the crash reporter in release builds.
* Never log state payloads that contain PII or secrets.

## 8. Testing

* Every Bloc/Cubit gets unit tests with `bloc_test`; mock repositories with `mocktail`.

```dart
blocTest<LoginBloc, LoginState>(
  'emits [loading, failure] when credentials are rejected',
  build: () {
    when(() => userRepository.login(any(), any()))
        .thenAnswer((_) async => Result.failure(AuthError.invalidCredentials));
    return LoginBloc(userRepository: userRepository);
  },
  act: (bloc) => bloc.add(const LoginSubmitted('a@b.com', 'wrong-password')),
  expect: () => [const LoginState.loading(), isA<LoginFailure>()],
);
```

* Tests must encode WHY the behavior matters (business rule), not just the emission sequence.

## 9. Anti-Patterns (Forbidden)

* `setState`-driven business logic, `StatefulWidget` state for anything a Bloc owns.
* Creating Blocs/repositories inside `build()`.
* Passing `BuildContext` into a Bloc, or importing Flutter widgets in Bloc files (Blocs are pure Dart).
* Emitting after an `await` without checking `isClosed` in long-lived subscriptions handled outside `on<Event>` (prefer `emit.forEach` for streams).
* God-Blocs that manage several screens; split per feature.
