using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Row-Level Security is the final, independent enforcement layer
    /// (AUTHORIZATION.md §11, DATABASE_RLS_POLICIES.md). The invariant
    /// plumbing (roles, get_current_user_id(), ENABLE/FORCE, worker
    /// bypass) comes from the CtxRls helpers so it stays version-locked
    /// to the RlsInterceptor; feature-specific predicates and policies
    /// are raw SQL (DATABASE_CODE_FIRST.md §5).
    /// </summary>
    public partial class AddRowLevelSecurity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            CtxRls.CreateInfrastructure(migrationBuilder);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION is_org_user(o_id bigint) RETURNS boolean
                LANGUAGE sql STABLE AS $$
                    SELECT EXISTS (
                        SELECT 1 FROM organizations o
                        WHERE o.id = o_id AND o.owner_id = get_current_user_id())
                $$;

                CREATE OR REPLACE FUNCTION is_project_member(p_id bigint) RETURNS boolean
                LANGUAGE sql STABLE AS $$
                    SELECT EXISTS (
                        SELECT 1 FROM member_users m
                        WHERE m.project_id = p_id AND m.id = get_current_user_id())
                $$;
                """);

            CtxRls.EnableForce(migrationBuilder,
                "users", "org_users", "member_users",
                "user_google_identity", "user_firebase_identity",
                "organizations", "projects", "member_invitations",
                "ledger", "orders",
                "user_totals", "project_totals",
                "user_notifications", "user_exports",
                "refresh_tokens", "app_instances", "signup_verifications");

            migrationBuilder.Sql("""
                CREATE POLICY user_self_policy ON users FOR ALL TO app_user
                    USING (id = get_current_user_id());
                CREATE POLICY org_user_access_policy ON org_users FOR ALL TO app_user
                    USING (id = get_current_user_id());

                CREATE POLICY member_user_select ON member_users FOR SELECT TO app_user
                    USING (id = get_current_user_id() OR is_org_user(org_id));
                CREATE POLICY member_user_update ON member_users FOR UPDATE TO app_user
                    USING (id = get_current_user_id() OR is_org_user(org_id));
                CREATE POLICY member_user_insert ON member_users FOR INSERT TO app_user
                    WITH CHECK (is_org_user(org_id));
                CREATE POLICY member_user_delete ON member_users FOR DELETE TO app_user
                    USING (is_org_user(org_id));

                CREATE POLICY user_google_identity_policy ON user_google_identity
                    FOR ALL TO app_user USING (user_id = get_current_user_id());
                CREATE POLICY user_firebase_identity_policy ON user_firebase_identity
                    FOR ALL TO app_user USING (user_id = get_current_user_id());

                CREATE POLICY org_access_policy ON organizations FOR ALL TO app_user
                    USING (is_org_user(id));
                CREATE POLICY project_access_policy ON projects FOR ALL TO app_user
                    USING (is_project_member(id) OR is_org_user(org_id));
                CREATE POLICY member_invitation_access_policy ON member_invitations
                    FOR ALL TO app_user
                    USING (is_project_member(project_id) OR is_org_user(org_id));

                CREATE POLICY ledger_select_policy ON ledger FOR SELECT TO app_user
                    USING (user_id = get_current_user_id());

                CREATE POLICY order_select_policy ON orders FOR SELECT TO app_user
                    USING (is_project_member(project_id));
                CREATE POLICY order_insert_policy ON orders FOR INSERT TO app_user
                    WITH CHECK (is_project_member(project_id));
                CREATE POLICY order_update_policy ON orders FOR UPDATE TO app_user
                    USING (is_project_member(project_id));
                CREATE POLICY order_delete_policy ON orders FOR DELETE TO app_user
                    USING (is_project_member(project_id));

                CREATE POLICY user_totals_policy ON user_totals FOR ALL TO app_user
                    USING (user_id = get_current_user_id());
                CREATE POLICY project_totals_policy ON project_totals FOR ALL TO app_user
                    USING (is_project_member(project_id));

                CREATE POLICY user_notifications_policy ON user_notifications
                    FOR ALL TO app_user USING (user_id = get_current_user_id());
                CREATE POLICY user_export_policy ON user_exports FOR ALL TO app_user
                    USING (user_id = get_current_user_id());
                CREATE POLICY refresh_token_policy ON refresh_tokens FOR ALL TO app_user
                    USING (user_id = get_current_user_id());

                CREATE POLICY app_instance_select ON app_instances FOR SELECT TO app_user
                    USING (true);
                CREATE POLICY app_instance_insert ON app_instances FOR INSERT TO app_user
                    WITH CHECK (true);
                CREATE POLICY app_instance_update ON app_instances FOR UPDATE TO app_user
                    USING (true);

                CREATE POLICY signup_verification_all ON signup_verifications
                    FOR ALL TO app_user USING (true) WITH CHECK (true);
                """);

            CtxRls.WorkerBypass(migrationBuilder,
                "users", "org_users", "member_users",
                "user_google_identity", "user_firebase_identity",
                "organizations", "projects", "member_invitations",
                "ledger", "orders",
                "user_totals", "project_totals",
                "user_notifications", "user_exports",
                "refresh_tokens", "app_instances", "signup_verifications");

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION notify_new_notification()
                RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_notify('new_notification', NEW.id::text);
                    RETURN NEW;
                END $$;

                CREATE TRIGGER user_notifications_notify
                    AFTER INSERT ON user_notifications
                    FOR EACH ROW EXECUTE FUNCTION notify_new_notification();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS user_notifications_notify ON user_notifications;
                DROP FUNCTION IF EXISTS notify_new_notification();
                DROP FUNCTION IF EXISTS is_project_member(bigint);
                DROP FUNCTION IF EXISTS is_org_user(bigint);
                """);
            CtxRls.DropInfrastructure(migrationBuilder);
        }
    }
}
