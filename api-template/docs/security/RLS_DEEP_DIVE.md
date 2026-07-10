# RLS Deep Dive: Implementation & Robustness

This document provides a technical deep dive into the Row-Level Security (RLS) enforcement mechanism used in the API.

## 1. The Challenge: Result Set Offset & Batching

In naive implementations of RLS interceptors, a common pattern is to prepend a `SET LOCAL` or `SELECT set_config(...)` command to the main query string.

**The Problem:** Prepending commands causes two major issues in EF Core:
1.  **Result Set Offset:** PostgreSQL returns the result of the `set_config` function (or a completion status for `SET LOCAL`) as a separate result set. EF Core expects the main query's results to be the first result set.
2.  **Batching Failure:** When EF Core executes multiple commands in a batch (e.g., creating a User, their aggregate totals, and a Welcome notification), it relies on the database returning the correct row-count for each operation in the batch. Prepending a command to the batch's SQL string confuses the mapping of result counts to entities, frequently leading to `DbUpdateConcurrencyException` (reporting 0 rows affected even when the row was inserted).

## 2. The Solution: Separate, Parameterized Connection-Scoped Commands

The API solves this by executing a dedicated, **parameterized** `set_config` command on the shared database connection immediately before the primary command.

### Implementation
The `RlsInterceptor` hooks into `Executing` events and establishes context like this:

```csharp
private void EnforceRls(DbCommand command, string userId)
{
    using var rlsCmd = command.Connection!.CreateCommand();
    rlsCmd.Transaction = command.Transaction;
    rlsCmd.CommandText = "SELECT set_config('app.current_user_id', @userId, true);";

    var p = rlsCmd.CreateParameter();
    p.ParameterName = "@userId";
    p.Value = userId;
    rlsCmd.Parameters.Add(p);

    rlsCmd.ExecuteNonQuery();
}
```

### Why this works:
1.  **No SQL Injection:** The identity value is bound as a query parameter. Interpolating it into the SQL string (e.g., `$"SET LOCAL app.current_user_id = '{userId}'"`) would open a SQL injection vector via the identity claim and is forbidden — this exact vulnerability class is recorded in the [Security Hardening Checklist](audits/SECURITY_HARDENING_CHECKLIST.md) §7. (`SET LOCAL` itself cannot take parameters, which is one more reason the parameterizable `set_config()` function is used.)
2.  **Result Isolation:** Because the command is executed as a completely separate `ExecuteNonQuery()` call, it doesn't interfere with the primary command's SQL text or its result mapping.
3.  **Transactional Integrity:** By explicitly assigning `rlsCmd.Transaction = command.Transaction`, the RLS setting is bound to the same transaction as the main command.
4.  **Batching Compatibility:** EF Core can batch its `INSERT/UPDATE` operations normally. The interceptor ensures the security context is "hot" on the connection before EF Core sends its batch.
5.  **No Identity Leakage:** The `is_local: true` argument scopes the setting to the current transaction, so it is discarded when the transaction ends or the connection returns to the pool.

## 3. Architecture: ICurrentUserProvider

To support RLS in various execution contexts (Web Requests, Background Jobs, Unit Tests), the identity retrieval is abstracted via `ICurrentUserProvider`.

- **Web Context:** Extracts the `uid` or `sub` claim from the `HttpContext.User`.
- **Background Jobs:** Allows manual override of the User ID context using `SetUserId(long)`. This is backed by `AsyncLocal` to ensure thread-safety in singleton services.

```csharp
// Example: Setting RLS context in a background job
using (var scope = serviceScopeFactory.CreateScope())
{
    var userProvider = scope.ServiceProvider.GetRequiredService<ICurrentUserProvider>();
    userProvider.SetUserId(targetUserId);
    
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    // RLS is now enforced for targetUserId
    var userData = await dbContext.Users.ToListAsync(); 
}
```

## 4. Robustness Features

The `RlsInterceptor` includes several safeguards:
- **Async/Sync Support:** Both execution paths are fully implemented to prevent deadlocks in high-throughput scenarios.
- **Transactional Assignment:** Explicitly shares the transaction object, ensuring RLS works during multi-step unit of work saves.
- **Logging:** Logs debug information when a query is executed without an identity context, aiding in troubleshooting authorization gaps.

## 5. PostgreSQL Policy Examples

Policies should always use `STABLE` functions for performance.

### Direct Ownership
```sql
CREATE POLICY user_ownership_policy ON users
    FOR SELECT
    TO app_user
    USING (id = get_current_user_id());
```

### Hierarchical (Project/Organization)
```sql
CREATE POLICY project_access_policy ON projects
    FOR SELECT
    TO app_user
    USING (is_project_member(id) OR is_org_user(org_id));
```

Prefer granular per-operation policies (`FOR SELECT`, `FOR INSERT`, `FOR UPDATE`, `FOR DELETE`) over monolithic `FOR ALL` — see [Security Hardening Checklist](audits/SECURITY_HARDENING_CHECKLIST.md) §8.

## 6. Security Considerations

- **Bypassing RLS:** RLS is only enforced for the application role (`app_user`). Superusers or the table owner (unless `FORCE ROW LEVEL SECURITY` is enabled) bypass RLS — which is why every table uses `FORCE`.
- **`SECURITY DEFINER` functions:** Always add `SET search_path = public` to `SECURITY DEFINER` helper functions to mitigate search-path hijacking.
- **Information Leakage:** Be careful with `USING` clauses that could leak information via timing attacks or error messages. Use `STABLE` functions to minimize the attack surface.
