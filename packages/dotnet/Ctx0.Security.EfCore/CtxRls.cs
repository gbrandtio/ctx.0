using Microsoft.EntityFrameworkCore.Migrations;

namespace Ctx0.Security.EfCore;

/// <summary>
/// Migration helpers emitting the Row-Level Security SQL that is
/// version-locked to the RlsInterceptor (AUTHORIZATION.md §10,
/// DATABASE_RLS_POLICIES.md): the roles, the get_current_user_id()
/// helper reading the interceptor's session setting, ENABLE/FORCE
/// toggles, owner policies, and the background-worker bypass policies.
/// Feature-specific policies (membership predicates, cross-table
/// visibility) stay hand-written SQL in the migration; these helpers
/// cover the invariant plumbing so it can never drift from the
/// interceptor's behavior.
/// </summary>
public static class CtxRls
{
    /// <summary>NOLOGIN policy role for authenticated app traffic.
    /// Rename target (`app_user`); must match production login-role
    /// membership (DATABASE_RLS_POLICIES.md §2).</summary>
    public const string UserRole = "app_user";

    /// <summary>NOLOGIN bypass role for background jobs; must match
    /// RlsOptions.WorkerRole.</summary>
    public const string WorkerRole = "app_internal_worker";

    /// <summary>Session setting the RlsInterceptor writes; must match
    /// RlsOptions.SettingName.</summary>
    public const string SettingName = "app.current_user_id";

    /// <summary>
    /// Roles, grants, and the STABLE get_current_user_id() function —
    /// run once before any policy is created.
    /// </summary>
    public static void CreateInfrastructure(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            DO $$ BEGIN CREATE ROLE {UserRole} NOLOGIN;
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;
            DO $$ BEGIN CREATE ROLE {WorkerRole} NOLOGIN;
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;

            DO $$ BEGIN
                EXECUTE format(
                    'GRANT {UserRole}, {WorkerRole} TO %I', current_user);
            EXCEPTION WHEN OTHERS THEN NULL; END $$;

            CREATE OR REPLACE FUNCTION get_current_user_id() RETURNS bigint
            LANGUAGE sql STABLE AS $$
                SELECT NULLIF(current_setting('{SettingName}', true), '')::bigint
            $$;

            -- Table privileges for the policy roles. RLS only ENGAGES
            -- for a non-superuser, non-owner session: production must
            -- connect as a login role that is a MEMBER of {UserRole} (a
            -- superuser or BYPASSRLS login would skip policies entirely).
            -- {WorkerRole} additionally gets privileges for the
            -- background-job bypass path.
            GRANT USAGE ON SCHEMA public TO {UserRole}, {WorkerRole};
            GRANT SELECT, INSERT, UPDATE, DELETE
                ON ALL TABLES IN SCHEMA public TO {UserRole}, {WorkerRole};
            ALTER DEFAULT PRIVILEGES IN SCHEMA public
                GRANT SELECT, INSERT, UPDATE, DELETE
                ON TABLES TO {UserRole}, {WorkerRole};
            """);
    }

    /// <summary>ENABLE + FORCE row level security on each table.</summary>
    public static void EnableForce(MigrationBuilder migrationBuilder, params string[] tables)
    {
        foreach (var table in tables)
        {
            migrationBuilder.Sql(
                $"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY; " +
                $"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;");
        }
    }

    /// <summary>
    /// FOR ALL owner policy: rows visible/writable only when
    /// <paramref name="ownerColumn"/> equals get_current_user_id().
    /// </summary>
    public static void OwnerPolicy(
        MigrationBuilder migrationBuilder, string table, string ownerColumn,
        string? policyName = null)
    {
        migrationBuilder.Sql(
            $"CREATE POLICY {policyName ?? $"{table}_owner_policy"} ON {table} " +
            $"FOR ALL TO {UserRole} USING ({ownerColumn} = get_current_user_id());");
    }

    /// <summary>internal_worker_bypass_&lt;table&gt; policy per table.</summary>
    public static void WorkerBypass(MigrationBuilder migrationBuilder, params string[] tables)
    {
        foreach (var table in tables)
        {
            migrationBuilder.Sql(
                $"CREATE POLICY internal_worker_bypass_{table} ON {table} " +
                $"TO {WorkerRole} USING (true) WITH CHECK (true);");
        }
    }

    public static void DropInfrastructure(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS get_current_user_id();");
    }
}
