# Caching Interceptor Specification

This document describes the implementation of the caching mechanism for the mobile client, utilizing Hive for high-performance persistence and a custom HTTP interceptor.

## Overview
The caching system is designed to improve performance and provide offline-first capabilities for `GET` requests. It operates as a layer between the `HttpApiService` and the network, managed by a custom `http.BaseClient` implementation.

## Tech Stack
- **Persistence**: [Hive](https://pub.dev/packages/hive) (NoSQL, binary storage).
- **Networking**: [http](https://pub.dev/packages/http).
- **Dependency Injection**: `ApiServiceFactory` (services are exposed to features via `RepositoryProvider`).

## Architecture

### 1. Data Model (`CacheEntry`)
Shipped by the `ctx0_mobile_security` package (`src/models/cache_entry.dart`).
Stores:
- `body`: The stringified response body.
- `statusCode`: The HTTP status code of the original response.
- `timestamp`: When the data was cached (used for TTL).
- `headers`: Original response headers (e.g., Content-Type).

### 2. Storage Service (`HiveCacheService`)
Shipped by the `ctx0_mobile_security` package (`src/storage/hive_cache_service.dart`).
Encapsulates all Hive operations:
- `init()`: Initializes Hive and registers the `CacheEntryAdapter`.
- `get(key)` / `put(key, entry)`: Standard CRUD operations.
- `delete(key)`: Targeted invalidation.
- `clear()`: Full cache purge.

### 3. The Interceptor (`CachingClient`)
Shipped by the `ctx0_mobile_security` package (`src/api/interceptors/caching_client.dart`).
Inherits from `http.BaseClient`. It intercepts every request and applies the following logic:

#### Execution Flow
1. **Bypass Check**: If the request contains the `X-Bypass-Cache` header, the cache is ignored, and a network call is forced.
2. **Method Filtering**: Only `GET` requests are candidates for caching.
3. **Check Cache**: Look for an entry matching the URL in Hive.
4. **TTL Validation (Cache First)**: If a cached entry is found and its age is `< 15 minutes`, return the cached response immediately (`x-from-cache: true`).
5. **Network Fallback**: If missing, expired, or forced, perform the real API call.
6. **Update Cache**: If the network response is successful (2xx), save it to Hive for future use.

## Invalidation Strategy
To ensure data consistency while using a Cache First strategy, the following invalidation rules are applied:

### 1. Mandatory Bypass (Non-cacheable)
Certain endpoints are explicitly excluded from caching by always including the `X-Bypass-Cache` header:
- **Real-time data**: Endpoints whose freshness is critical (e.g., `GET /v1/users/notifications/...`) are always fetched from the network.
- **Auth endpoints**: Token and session endpoints are never cached (see `../../docs/features/AUTHENTICATION.md`).

### 2. Event-Driven Invalidation
When the client performs state-changing operations, it automatically invalidates the cached patterns of every resource the operation affects. Examples:
- **Successful payment/order**: Invalidates the user's balance/stats endpoints and the history list (`/orders/`).
- **User Profile Update (`updateUser`)**: Invalidates the user details (`/users/{id}`) and related aggregates.
- **Account Deletion**: Performs a full cache purge.

**Rule**: Any new mutating endpoint MUST document and implement the list of cache patterns it invalidates.

### 3. Manual Force Refresh
The application supports manual "Pull-to-Refresh" on primary screens. This action triggers a `forceRefresh` flag which adds the `X-Bypass-Cache` header to all underlying requests for that screen, ensuring the user can manually sync with the backend.

## Usage Guide

### Standard Request (Auto-cached)
No special configuration is needed. All `GET` calls through `ApiService` use the Cache-First strategy by default.
```dart
final result = await apiService.getUserProfile(userId); // Uses cache if < 15m old
```

### Force Refresh (Bypass)
To ignore the cache and ping the real API (e.g., pull-to-refresh):
```dart
// The X-Bypass-Cache header is stripped before reaching the server
final result = await apiService.getUserProfile(userId, forceRefresh: true);
```

### Manual Invalidation
To clear a specific endpoint pattern (e.g., after an update):
```dart
final cachingClient = client as CachingClient;
await cachingClient.invalidatePattern('/users/$userId');
```


## Security & Performance
- **Non-sensitive data**: Only non-sensitive API responses should be cached. Sensitive user tokens remain in `SecureStorageService`.
- **TTL**: A default 15-minute TTL is enforced to prevent stale data while maintaining performance.
- **Header Injection**: Responses served from the cache include an `x-from-cache: true` header for debugging and UI feedback.
