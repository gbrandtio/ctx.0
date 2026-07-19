using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CtxApp.Infrastructure.Security.Rls;

/// <summary>Declares a table that must be Row-Level-Security scoped to its owner column.</summary>
public sealed record RlsPolicy(string Table, string UserColumn);

/// <summary>Applies PostgreSQL Row-Level Security policies keyed on the <c>app.user_id</c> session variable.</summary>
public static class CtxRls
{
    /// <summary>
    /// Enable and FORCE row-level security on <paramref name="table"/>, with a
    /// policy that only exposes rows whose <paramref name="userColumn"/> equals
    /// the current <c>app.user_id</c>. Idempotent; the table must already exist.
    /// Table/column names are fixed by the schema, not user input.
    /// </summary>
    public static Task EnableAsync(DatabaseFacade database, string table, string userColumn, CancellationToken ct = default)
    {
        var predicate = $"\"{userColumn}\" = NULLIF(current_setting('app.user_id', true), '')::uuid";
        var sql = $"""
        ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
        ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
        DROP POLICY IF EXISTS {table}_isolation ON "{table}";
        CREATE POLICY {table}_isolation ON "{table}" USING ({predicate}) WITH CHECK ({predicate});
        """;
        return database.ExecuteSqlRawAsync(sql, ct);
    }
}
