# EF Core Advanced Performance Enforcements

**Version:** 1.1
**Target Audience:** LLM Agents, Code Generators, Refactoring Tools
**Objective:** Enforce top-percentile performance consistency, minimal runtime overhead, and clean architecture principles in .NET (C#) data-access layers using Entity Framework Core.

---

## 1. DbContext Pooling (`AddDbContextPool`)

**Context:** Instantiating `DbContext` is fast, but the continuous allocation of internal services under high-throughput loads creates measurable Garbage Collection (GC) pressure and setup latency.

**Rule:** Always default to `DbContext` pooling for web applications and APIs unless stateful context requirements prevent it.

* **DO:** Use `AddDbContextPool` in dependency injection setups.
* **DON'T:** Use the standard `AddDbContext` in high-concurrency scenarios without explicitly justifying the fallback.
* **CONSTRAINT:** When state (e.g., multi-tenant IDs) changes per request, `OnConfiguring` cannot be used since pooled contexts are treated as Singletons. You MUST inject an `IDbContextFactory<T>` and manually hydrate the state after retrieving the context from the pool.

```csharp
// Enforced pattern for DI (PostgreSQL example)
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

---

## 2. Query Caching and Parameterization

**Context:** EF Core caches query execution plans based on the expression tree shape. Inlining dynamic variables into LINQ queries causes cache bloat and forces continuous, expensive query recompilation.

**Rule:** Strictly enforce parameterization.

* **DO:** Pass variables into LINQ queries so EF Core generates parameterized SQL (`@p0`).
* **DON'T:** Construct dynamic LINQ queries by concatenating strings or using constant injection that changes per request.
* **EXCEPTION (`EF.Constant`):** If a query contains a value that rarely changes and the database query optimizer needs to see the literal value to utilize indexes properly, force it using the Expression API.

```csharp
// Forces a hardcoded constant in the generated SQL instead of a parameter
var users = await context.Users
    .Where(u => u.Status == EF.Constant(Status.Inactive))
    .ToListAsync();
```

---

## 3. Compiled Queries (`EF.CompileAsyncQuery`)

**Context:** Even with query caching, EF Core must recursively hash and compare the incoming LINQ expression tree against the cache. For ultra-low-latency, high-frequency queries, this hashing overhead becomes a bottleneck.

**Rule:** For the most heavily utilized read paths in the system, bypass the expression tree comparison entirely using explicitly compiled queries.

* **DO:** Store the compiled query as a `static readonly` delegate.
* **DON'T:** Use compiled queries for complex, dynamically built queries (e.g., search pages with multiple optional filters). They are strictly for static-shaped, high-volume queries.

```csharp
// Enforced pattern for high-frequency data access
private static readonly Func<AppDbContext, int, IAsyncEnumerable<User>> _getUsersByRole =
    EF.CompileAsyncQuery((AppDbContext context, int roleId) =>
        context.Users.AsNoTracking().Where(u => u.RoleId == roleId));

public async Task<List<User>> GetUsersByRoleAsync(int roleId)
{
    var users = new List<User>();
    await foreach (var user in _getUsersByRole(_context, roleId))
    {
        users.Add(user);
    }
    return users;
}
```

---

## 4. Compiled Models

**Context:** Applications with large domains suffer from severe startup latency due to EF Core discovering and building the metadata model on the first query.

**Rule:** Pre-compile the EF Core model to optimize startup time and reduce memory footprint.

* **DO:** Ensure CI/CD pipelines generate the compiled models using the .NET CLI (`dotnet ef dbcontext optimize`).
* **DO:** Configure the `DbContext` to use the generated model in the application startup phase.

```csharp
// Injects the compiled model
builder.Services.AddDbContextPool<AppDbContext>(options =>
    options
        .UseModel(AppDbContextModel.Instance) 
        .UseNpgsql(connectionString));
```

---

## 5. Reducing Runtime Overhead & Memory Allocations

**Context:** Tracking entities requires EF Core to maintain identity maps and snapshots, drastically increasing memory allocation.

**Rule:** Read-only queries must never track state.

* **DO:** Append `.AsNoTracking()` to all read-only queries.
* **DO:** Use `.Select()` to project exactly the columns needed into DTOs or ViewModels.
* **DON'T:** Fetch complete entities if only a subset of fields (like `Id` and `CreatedAt`) are required by the business logic.

---

## 6. Set-Based Writes & Split Queries

**Context:** Loading entities just to mutate or delete them wastes round-trips and memory; multi-collection `Include` chains generate cartesian-explosion joins.

* **DO:** Use `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for set-based mutations that don't need domain logic per entity. These are also the required mechanism for atomic counter increments and single-use flag flips (see [Security Hardening Checklist](../security/audits/SECURITY_HARDENING_CHECKLIST.md) §2).

```csharp
await context.PaymentCodes
    .Where(c => c.ExpiresAt < now && !c.IsInvalidated)
    .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsInvalidated, true));
```

* **DO:** Use `.AsSplitQuery()` when including **multiple collection navigations** to avoid cartesian explosion; keep the default single query for one-collection includes.
* **DON'T:** Use `ExecuteUpdate/Delete` for entities whose changes must trigger interceptors (e.g., envelope-encrypted PII columns) — those must go through the normal `SaveChanges` pipeline.
