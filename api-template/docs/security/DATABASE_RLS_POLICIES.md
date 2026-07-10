# Database Row-Level Security (RLS) Policies

This document provides a detailed list of all Row-Level Security (RLS) policies applied to the PostgreSQL database tables. These policies act as the final enforcement layer, ensuring that users can only access data they are authorized to see, even if application-level checks are bypassed.

The tables below reflect the template's example hierarchy (`users`, `organizations` → `projects`, orders). When you rename or add aggregates, mirror the appropriate pattern.

---

## 1. Core Identity Functions

The following helper functions are used across all policies to ensure optimized and consistent identity extraction. All are declared `STABLE` (see [Row-Level Security](ROW_LEVEL_SECURITY.md) §2) and, where `SECURITY DEFINER` is used, include `SET search_path = public`.

| Function | Description |
| :--- | :--- |
| `get_current_user_id()` | Retrieves the `app.current_user_id` session variable set by the API's `RlsInterceptor`. |
| `is_org_user(o_id)` | Checks if the current user is an `OrgUser` authorized for the given organization ID. |
| `is_project_member(p_id)` | Checks if the current user is a `MemberUser` authorized for the given project ID. |

---

## 2. Table-Specific Policies

All policies are applied with `FORCE ROW LEVEL SECURITY` to ensure they apply even to the table owner. Prefer granular per-operation policies (`FOR SELECT` / `FOR INSERT` / `FOR UPDATE` / `FOR DELETE`) over `FOR ALL` — monolithic policies have caused horizontal privilege escalation before (see [Security Hardening Checklist](audits/SECURITY_HARDENING_CHECKLIST.md) §8).

### User & Identity Tables

| Table | Policy Name | Access Logic (USING Clause) |
| :--- | :--- | :--- |
| `users` | `user_self_policy` | `id = get_current_user_id()` |
| `org_users` | `org_user_access_policy` | `id = get_current_user_id()` |
| `member_users` | `member_user_access_policy` | `SELECT`/`UPDATE`: `id = get_current_user_id() OR is_org_user(org_id)`<br>`INSERT`/`DELETE`: `is_org_user(org_id)` |
| `user_google_identity` | `user_google_identity_policy` | `user_id = get_current_user_id()` |
| `user_firebase_identity` | `user_firebase_identity_policy` | `user_id = get_current_user_id()` |

### Business Entity Tables

| Table | Policy Name | Access Logic (USING Clause) |
| :--- | :--- | :--- |
| `organizations` | `org_access_policy` | `is_org_user(id)` |
| `projects` | `project_access_policy` | `is_project_member(id) OR is_org_user(org_id)` |
| `member_invitations` | `member_invitation_access_policy` | `is_project_member(project_id) OR is_org_user(org_id)` |

### Transactional & Ledger Tables

| Table | Policy Name | Access Logic (USING Clause) |
| :--- | :--- | :--- |
| `ledger` | `ledger_select_policy` | `user_id = get_current_user_id()` (SELECT only) |
| `orders` | Granular Policies | `SELECT`: `is_project_member(project_id)`<br>`INSERT`/`UPDATE`/`DELETE`: `is_project_member(project_id)` |

### Aggregate / Analytics Tables

| Table | Policy Name | Access Logic (USING Clause) |
| :--- | :--- | :--- |
| `user_totals` | `user_totals_policy` | `user_id = get_current_user_id()` |
| `project_totals` | `project_totals_policy` | `is_project_member(project_id)` |

### Notification & Support Tables

| Table | Policy Name | Access Logic (USING Clause) |
| :--- | :--- | :--- |
| `user_notifications` | `user_notifications_policy` | `user_id = get_current_user_id()` |
| `user_exports` | `user_export_policy` | `user_id = get_current_user_id()` |
| `refresh_tokens` | `refresh_token_policy` | `user_id = get_current_user_id()` |
| `app_instances` | Granular Policies | `SELECT`: `true`<br>`INSERT`: `true` (Registration only) |
| `signup_verifications` | Granular Policies | `SELECT`/`INSERT`/`UPDATE`/`DELETE`: `true` (Anonymous 2FA) |

---

## 3. System Bypass Policy

A dedicated role `app_internal_worker` is created for system-level background tasks (e.g., automated cleanups, KEK rotation, cross-owner payment processing) that require visibility across all rows.

For **every table** listed above, an additional policy is created:
*   **Name**: `internal_worker_bypass_<table_name>`
*   **Target**: `TO app_internal_worker`
*   **Logic**: `USING (true)`

The API switches to this role via `SET LOCAL ROLE app_internal_worker` when `ICurrentUserProvider.IsSystemBypassActive()` is true.

---

## 4. Maintenance

The source of truth for these policies is the **EF Core migrations** in `Infrastructure/Persistence/Migrations/` — RLS helper functions, `ENABLE`/`FORCE` statements, roles, and policies are applied via `migrationBuilder.Sql(...)` (see the [Database Code-First Guide](../architecture/DATABASE_CODE_FIRST.md)).

Any schema change that adds a table holding user-owned data **must** ship, in the same migration:
1. The RLS `ENABLE`/`FORCE` statements and policies for the new table.
2. The `internal_worker_bypass_<table>` policy.
3. An update to this document.
