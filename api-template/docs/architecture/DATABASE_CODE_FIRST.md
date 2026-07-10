# Database: EF Core Code-First Guide

The database schema is **code-first**: the EF Core model plus its **migrations** in `Infrastructure/Persistence/Migrations/` are the single source of truth for everything in the database — tables, indexes, constraints, PostGIS setup, RLS policies, roles, and seed data. There are no parallel hand-maintained SQL schema scripts.

---

## 1. Golden Rules

1. **Every schema change is a migration.** Change the entity/configuration, then run `dotnet ef migrations add <DescriptiveName>`. Never hand-edit the database, and never edit an already-applied migration — add a new one.
2. **No `EnsureCreated()`**, anywhere, ever (it bypasses migrations and makes the database un-migratable). Tests use migrations too (see Section 7).
3. **Review the generated migration** before committing. The scaffolder is not infallible — verify destructive operations (column drops, type changes) are intended and data-safe.
4. **RLS and roles ride inside migrations** via `migrationBuilder.Sql(...)` — see Section 5.
5. **Additive first (zero downtime).** Deployments run migrations before swapping instances, so a migration must be compatible with the *previous* app version still running: add columns as nullable/defaulted, backfill, then tighten in a later migration; never rename in one step (add new → copy → drop old).

## 2. Model Configuration Conventions

- One `IEntityTypeConfiguration<T>` class per entity in `Infrastructure/Persistence/Configurations/`. No data annotations in the Domain layer beyond what [ADR 0001](adrs/0001-domain-layer-persistence-awareness.md) permits.
- **snake_case naming**: table and column names use `lowercase_snake_case` (via the `EFCore.NamingConventions` package: `options.UseSnakeCaseNamingConvention()`), matching PostgreSQL conventions and the names referenced by RLS policies.
- **UTC timestamps**: all date columns are `timestamp with time zone`; values are always `DateTime.UtcNow` (see ARCHITECTURE_OVERVIEW → Global Coding Standards).
- **Foreign keys are mandatory** for every relationship — declared via navigation properties or explicit `HasOne/WithMany` so migrations generate the constraints (see [Security Hardening Checklist](../security/audits/SECURITY_HARDENING_CHECKLIST.md) §10).
- **Indexes** are declared in the configuration (`builder.HasIndex(...)`), including unique indexes for blind-index hash columns (e.g., `email_hash`).
- **Encrypted PII columns** are configured as `text`/`bytea` with their value converters; searchable PII gets a companion `*_hash` blind-index column (see [Envelope Encryption](../security/ENVELOPE_ENCRYPTION_ARCHITECTURE.md)).

## 3. Workflow

```bash
# 1. Modify entities / configurations
# 2. Add the migration (run from the solution root)
dotnet ef migrations add AddPlaceOpeningHours --project Infrastructure --startup-project AppApi

# 3. Inspect the generated Up()/Down() methods

# 4. Apply locally
dotnet ef database update --project Infrastructure --startup-project AppApi
```

### Applying migrations in deployment
Generate a **migration bundle** in CI and execute it as a deploy step, *before* the new app version starts:

```bash
dotnet ef migrations bundle --project Infrastructure --startup-project AppApi --self-contained -o migrate
./migrate --connection "$DATABASE_URL"
```

Do **not** call `dbContext.Database.Migrate()` from application startup in multi-instance deployments — concurrent instances racing to migrate is a known failure mode. (It is acceptable for single-instance/dev setups.)

## 4. PostGIS (Spatial) Setup

Spatial support is configured code-first:

```csharp
// DI registration
options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite());

// AppDbContext.OnModelCreating
modelBuilder.HasPostgresExtension("postgis");

// Entity configuration — geography (meters), not geometry (degrees)
builder.Property(p => p.Location).HasColumnType("geography (point)");
builder.HasIndex(p => p.Location).HasMethod("GIST");
```

The `HasPostgresExtension` call makes the initial migration emit `CREATE EXTENSION IF NOT EXISTS postgis;` (the database user needs the privilege once, or pre-create the extension in provisioning). Query patterns are documented in [Spatial Queries](../features/SPATIAL_QUERIES.md).

## 5. RLS, Roles & Raw SQL in Migrations

Row-Level Security cannot be expressed in the EF model, so it is applied with `migrationBuilder.Sql(...)` inside dedicated, clearly named migrations (e.g., `AddRlsCoreTables`):

```csharp
public partial class AddRlsCoreTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Roles (idempotent)
            DO $$ BEGIN
                CREATE ROLE app_user NOLOGIN;
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;
            DO $$ BEGIN
                CREATE ROLE app_internal_worker NOLOGIN;
            EXCEPTION WHEN duplicate_object THEN NULL; END $$;

            -- STABLE helper functions
            CREATE OR REPLACE FUNCTION get_current_user_id() RETURNS bigint
            LANGUAGE sql STABLE
            AS $$ SELECT NULLIF(current_setting('app.current_user_id', true), '')::bigint $$;

            -- Enable + force RLS
            ALTER TABLE users ENABLE ROW LEVEL SECURITY;
            ALTER TABLE users FORCE ROW LEVEL SECURITY;

            -- Granular policies (never FOR ALL — see hardening checklist §8)
            CREATE POLICY user_self_select ON users FOR SELECT TO app_user
                USING (id = get_current_user_id());
            CREATE POLICY internal_worker_bypass_users ON users TO app_internal_worker
                USING (true);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP POLICY IF EXISTS user_self_select ON users;
            DROP POLICY IF EXISTS internal_worker_bypass_users ON users;
            ALTER TABLE users DISABLE ROW LEVEL SECURITY;
            """);
    }
}
```

Rules:
- A migration adding a table with user-owned data **must include** its RLS enablement, policies, and the `internal_worker_bypass_*` policy in the same migration, and update [Database RLS Policies](../security/DATABASE_RLS_POLICIES.md).
- Helper functions use `CREATE OR REPLACE` and are `STABLE`; `SECURITY DEFINER` functions must set `SET search_path = public`.
- Triggers (e.g., the `new_notification` LISTEN/NOTIFY trigger, see [Notifications](../features/NOTIFICATIONS.md)) follow the same pattern.

## 6. Seeding

- **Reference/static data** (e.g., country lists): use `builder.HasData(...)` in entity configurations so seeds are versioned inside migrations.
- **Environment-specific data** (demo accounts, test fixtures): a dedicated `IHostedService` seeder gated to Development — never `HasData` (it would ship to production).

## 7. Testing the Persistence Layer

- **Integration tests** run against real PostgreSQL via **Testcontainers**, applying the full migration set (`dbContext.Database.MigrateAsync()`), so RLS policies and PostGIS behavior are exercised for real.
- Do not test persistence against the InMemory provider — it validates nothing about SQL, RLS, or constraints.

## 8. Concurrency Tokens

For aggregates where lost updates matter and atomic SQL increments don't apply, use PostgreSQL's `xmin` as an optimistic concurrency token:

```csharp
builder.Property<uint>("xmin").IsRowVersion();
```

Prefer `ExecuteUpdateAsync` atomic operations for counters (see [EF Core Performance](../performance/EFCORE_ADVANCED_PERFORMANCE_TOPICS.md) §6); reserve concurrency tokens for multi-field entity edits.
