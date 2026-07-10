# Flutter App Architecture Guide

*Ref: [docs.flutter.dev/app-architecture](https://docs.flutter.dev/app-architecture) and [bloclibrary.dev/architecture](https://bloclibrary.dev/architecture/)*

This document defines the architecture for building scalable, maintainable, and testable applications with **Bloc** as the state management solution. It details the core layers, project structure, and specific design patterns to solve common problems. For Bloc-specific conventions (Cubit vs Bloc, state modeling, testing), see `docs/STATE_MANAGEMENT.md`.

## 1. Core Architectural Principles

* **Separation of Concerns**: Divide the application into distinct layers (UI, Data, Domain).
* **Layered Architecture**: Layers only communicate with their immediate neighbours (UI ↔ Data).
* **Single Source of Truth (SSOT)**: The Data Layer (Repository) is the absolute authority for data, managing the coordination between local cache and remote server.
* **Unidirectional Data Flow (UDF)**: Data flows **down** (Data → UI), and events flow **up** (UI → Data).
* **UI is a Function of State**: The UI merely reflects the current Bloc state; it does not manage business logic.

## 2. Architectural Layers

### A. UI Layer (Presentation)
Handles displaying data and capturing user input. Follows the **Bloc** pattern: widgets dispatch events, Blocs emit states, widgets rebuild from states.

* **Views (Widgets)**:
    * **Role**: Render UI, handle user gestures by adding events to the Bloc (`context.read<XBloc>().add(...)`).
    * **Logic**: Strictly presentation logic (animations, navigation). No business logic.
    * **Dependencies**: `Bloc`/`Cubit` (via `BlocProvider` / `BlocBuilder`).
* **Blocs / Cubits**:
    * **Role**: Manage UI state and presentation-level business logic for a specific feature.
    * **Logic**: Receive events (or method calls, for Cubits), call Repositories, convert domain data + `Result` outcomes into immutable UI states (loading / success / failure), and emit them.
    * **Dependencies**: `Repository` (or `UseCase`). Never other Blocs' internals, Bloc-to-Bloc communication goes through repositories or stream subscriptions (see `docs/STATE_MANAGEMENT.md`).

### B. Data Layer
Manages application data, networking, and persistence.

* **Repositories**:
    * **Role**: The public interface for data access. Acts as the **SSOT**.
    * **Logic**: Coordination (e.g., "Check local DB first, then fetch API"), data mapping (DTO → Domain Model).
    * **Dependencies**: `Services`.
* **Services (Data Sources)**:
    * **Role**: Low-level implementation of data fetching.
    * **Types**:
        * **API Service**: Wraps HTTP calls (e.g., `http`).
        * **Local Storage Service**: Wraps database/prefs (e.g., `SharedPrefs`, `Drift`, `Hive`).

### C. Domain Layer (Optional)
Used for complex business rules that span multiple Blocs.

* **UseCases**: Encapsulate reusable business logic (e.g., `CalculateTaxUseCase`).

## 3. Common Design Patterns

### A. Event → State Loading Discipline (Safe UI Actions)

Every asynchronous user action follows the same state discipline inside the Bloc:

1. On receiving the event, immediately emit a **loading** state.
2. Perform the async work via the Repository.
3. Emit **success** or **failure**.

* **Why**: Prevents "double-tap" issues (the View disables the button while the state is loading), standardises loading/error handling, and keeps Views dumb.
* **View side**: Bind button `onPressed` to `bloc.add(SubmitPressed())`; disable the button when `state is XLoading`.
* **Concurrency**: For events that must not run concurrently (e.g., form submission), use the `droppable()` event transformer from `bloc_concurrency`; for search-as-you-type use `restartable()`. Never rely on boolean guard flags inside widgets.

### B. Result Pattern (Error Handling)
*Ref: [Error handling with Result](https://docs.flutter.dev/app-architecture/design-patterns/result)*

Avoid throwing exceptions from the Data Layer to the UI Layer. Instead, return a `Result` type.

* **Structure**:
    * `sealed class Result<T>` with subclasses `Success<T>` and `Failure<E>`.
* **Flow**:
    * **Repository**: Catches low-level exceptions (e.g., `SocketException`) and returns `Result.failure(NetworkError)`.
    * **Bloc**: Uses a switch statement on the `Result` to emit the corresponding success or failure state explicitly.

### C. Optimistic State (User Experience)
*Ref: [Optimistic State](https://docs.flutter.dev/app-architecture/design-patterns/optimistic-state)*

For actions like "Like", "Subscribe", or "Todo Toggle", update the UI *immediately* before the server responds.

* **Mechanism**:
    1.  **User Action**: User taps "Like"; the View adds `LikePressed` to the Bloc.
    2.  **Immediate Update**: Bloc emits a state with `isLiked = true`.
    3.  **Async Call**: Bloc calls `repository.likeItem()`.
    4.  **Rollback**: If the call fails, the Bloc emits a state with `isLiked = false` and a failure flag; the View shows an error Snackbar via `BlocListener`.

## 4. Persistence & Offline Strategy

*Ref: [Offline-first Support](https://docs.flutter.dev/app-architecture/design-patterns/offline-first)*

### Storage Types
1.  **Key-Value (`shared_preferences`)**:
    * **Use Case**: Simple non-sensitive flags (Theme, Onboarding finished). **Never tokens or secrets** which must go to secure storage (see `docs/SECURITY.md`).
    * **Pattern**: Wrap `SharedPreferences` in a `Service` class. Inject into Repository.
2.  **SQL (`sqflite` / `drift`)**:
    * **Use Case**: Structured, relational data (Todo lists, Feed posts, User profiles).
    * **Pattern**: Use a `DatabaseService` to execute SQL queries. Repository converts `LocalDTO` (database row) to `DomainModel`.

### Offline Strategies (Repository Logic)
The Repository decides how to combine Local and Remote data.

* **Strategy 1: Local Fallback (Read)**:
    * Try fetching from Network.
    * If Network fails (no internet), fetch from Local DB.
    * Return data (or error if both fail).
* **Strategy 2: Stream / Sync (Read - Recommended)**:
    * Repository exposes a `Stream<Data>`; the Bloc subscribes to it (`emit.forEach` / `on<_DataUpdated>`).
    * **Emit 1**: Immediately emit data from Local DB (fast UI).
    * **Background**: Fetch fresh data from Network.
    * **Save**: Persist fresh data to Local DB.
    * **Emit 2**: The Local DB update triggers a new Stream emission with fresh data.

## 5. Project Structure

Recommended folder structure for a scalable app:

```text
lib/
├── main.dart
├── core/                     # Shared utilities, extensions, UI widgets
│   ├── utils/
│   └── widgets/
├── data/                     # Global data implementation
│   ├── services/             # (e.g. ApiService, DatabaseService, SharedPreferencesService)
│   └── repositories/         # (e.g. AuthRepository)
└── features/
    └── feature_name/         # (e.g. 'login', 'feed')
        ├── bloc/             # Bloc/Cubit, events, states (one Bloc per feature screen)
        ├── data/             # Feature-specific DTOs/Repositories
        └── views/            # Widgets (Screens & Components)
```

## 6. Dependency Injection & Reactive Optimization

To prevent "Request Spikes" and ensure efficient background operation, the following rules must be followed:

### A. Dependency Lifecycle Management
*   **Rule**: Provide repositories once, near the root, with `RepositoryProvider` / `MultiRepositoryProvider`. Provide Blocs with `BlocProvider` at the narrowest scope that needs them (usually the feature route). Never construct Blocs or repositories inside `build()` bodies because this recreates them on every rebuild and triggers redundant initialization logic and API calls.
*   **Rule**: Use `context.read<T>()` for one-off access (callbacks, event handlers) and `context.watch<T>()` / `BlocBuilder` only where the widget genuinely must rebuild on state changes.

### B. Rebuild Scope Control
*   **Rule**: Use `buildWhen` on `BlocBuilder` (and `listenWhen` on `BlocListener`) to skip rebuilds when the relevant slice of state has not changed. Prefer `BlocSelector` to rebuild on a single derived value.
*   **Rule**: Keep states immutable and equatable (via `freezed`/`equatable`); Bloc automatically skips emitting states equal to the current one, which prevents reactive loops for free.

### C. Reactive Loop Prevention
When a Bloc reactively listens to a Repository stream, a reload loop can occur if handling the update itself modifies the repository state.
*   **Rule**: Repository writes triggered by a stream update must not re-emit an identical value; compare-before-write in the Repository, or model the refresh as a distinct Bloc event handled with a `droppable()` transformer so overlapping refreshes are ignored.

### D. State Preservation during Refreshes
Background processes, such as proactive JWT token refreshes, often receive "bare" data models from the server that lack joined statistics (e.g., aggregate counters).
*   **Rule**: When updating the "Current User" from a background refresh, use `.copyWith` to preserve existing local stats. Never overwrite the local user with an incomplete model, as this triggers unnecessary state emissions and reload cascades.
