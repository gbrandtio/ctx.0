# Caching Strategy

The API employs a multi-tiered caching strategy to maximize responsiveness and minimize database load, especially for high-traffic read operations.

---

## 1. Output Caching (ASP.NET Core)

Output caching is the primary mechanism for reducing API latency. It caches the entire HTTP response (including headers and body) for a specific duration.

### Implementation Details
- **Provider**: Standard ASP.NET Core Output Caching middleware.
- **Default Policy**: 30 seconds expiration.
- **Scope**: Only applied to **public, non-personalized** endpoints. By default the middleware does **not** cache authenticated requests or responses that set cookies — do not weaken this, as caching personalized responses is a data-leak vector.
- **Methods**: Output caching applies to `GET`/`HEAD` only. Cacheable read endpoints must therefore be modeled as `GET` with query parameters (e.g., nearby search takes `lat`/`lng`/`radius` as query string), not `POST`.
- **Middleware Order**: `UseOutputCache()` is registered **after** `UseAuthentication()`/`UseAuthorization()` in the pipeline so cache-policy decisions can observe the authenticated state.

### Configuration in `Program.cs`
```csharp
builder.Services.AddOutputCache(o => 
    o.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(30))));

// ...

app.UseOutputCache();
```

### Targeted Endpoints
Endpoints that benefit most from output caching (all public, all `GET`):
- **`GET /v1/items/nearby?lat=..&lng=..&radiusKm=..`**: Cached (varying by query) to prevent redundant spatial queries for users in the same vicinity.
- **`GET /v1/countries`**: Reference data, cached as it changes very infrequently.
- **Public directory listings** (e.g., `GET /v1/organizations`).

---

## 2. In-Memory Caching (`IMemoryCache`)

For data that is frequently accessed across different requests but doesn't warrant a full response cache, the API uses `IMemoryCache`.

### Use Cases
- **Configuration Secrets**: Cached after the first decryption to avoid repeated AES/KMS overhead.
- **Static Domain Data**: Constants and calculation factors.
- **Rate Limiting Tokens**: The internal state of the rate limiter uses memory-efficient counters.

---

## 3. Cache Invalidation Rules

Since the current implementation uses a 30-second fixed window for Output Caching, invalidation is primarily **time-based**. If a cached endpoint's data must reflect a mutation sooner, tag the policy (`.Tag("items")`) and evict via `IOutputCacheStore.EvictByTagAsync` in the mutating handler.

### When to Bypass Cache
Clients can bypass the cache (if configured in the policy) using the `Cache-Control: no-cache` header, though this is typically restricted to administrative users to prevent "cache-busting" attacks.

---

## 4. Performance Impact

Indicative measurements (single instance, warm database):

| Layer | Latency (No Cache) | Latency (Cached) | Improvement |
|---|---|---|---|
| **Spatial Query** | 150ms - 300ms | 5ms - 15ms | ~95% |
| **Authentication** | 50ms - 100ms | N/A (Never Cached) | - |
| **Directory List** | 40ms - 80ms | 2ms - 5ms | ~90% |

---

## 5. Future Scalability: Distributed Caching (Redis)

While `IMemoryCache` and standard Output Caching are sufficient for single-instance deployments, a transition to **Redis-backed Distributed Caching** is the natural next step for multi-instance scaling. This allows:
1. **Shared Rate Limiting**: Consistent limits across all API nodes.
2. **Global Invalidation**: One node can invalidate a cache key for all other nodes.
3. **Shared Output Cache**: `AddStackExchangeRedisOutputCache` makes the response cache consistent across instances.
