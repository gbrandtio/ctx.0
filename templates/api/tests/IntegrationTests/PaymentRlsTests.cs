using Domain.Entities;
using Npgsql;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Proves the payment flow works under the production RLS posture (C1/H1).
/// A consumer is a plain `users` identity, not a project member, so the
/// order is invisible under their own RLS context — and the anonymous
/// webhook has no identity at all. Fulfillment therefore runs as the
/// internal worker role inside a transaction (exactly what the endpoints
/// now do). These tests reproduce both halves against real policies using
/// the same SET LOCAL ROLE idiom as RlsIsolationTests.
/// </summary>
public sealed class PaymentRlsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private async Task SeedPendingOrderAsync(long orderId, long projectId, long amountMinor)
    {
        // Seeded via the superuser context (bypasses RLS) — the equivalent
        // of a member actor having created the order out of band. The order
        // FKs a member_user → project → organization → org-user owner, so the
        // whole graph is created with ids derived from the order id.
        var ownerId = orderId + 1;
        var orgId = orderId + 2;
        var memberId = orderId + 3;
        await using var db = _factory.NewDbContext();
        db.OrgUsers.Add(new OrgUser
        {
            Id = ownerId, Email = $"owner{orderId}@x.test", EmailHash = $"oeh{orderId}",
            PasswordHash = "x", Type = "owner", CreatedAt = DateTime.UtcNow,
        });
        db.Organizations.Add(new Organization
        {
            Id = orgId, Name = $"org{orderId}", OwnerId = ownerId, CreatedAt = DateTime.UtcNow,
        });
        db.Projects.Add(new Project
        {
            Id = projectId, OrgId = orgId, Name = $"proj{orderId}", CreatedAt = DateTime.UtcNow,
        });
        db.MemberUsers.Add(new MemberUser
        {
            Id = memberId, OrgId = orgId, ProjectId = projectId,
            Email = $"member{orderId}@x.test", EmailHash = $"meh{orderId}", PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
        });
        db.Orders.Add(new Order
        {
            Id = orderId,
            ProjectId = projectId,
            CreatedByMemberUserId = memberId,
            AmountMinor = amountMinor,
            Currency = "eur",
            Status = Order.Statuses.Pending,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedUserAsync(long userId)
    {
        await using var db = _factory.NewDbContext();
        db.Users.Add(new User
        {
            Id = userId, Username = $"consumer{userId}", Email = $"consumer{userId}@x.test",
            UsernameHash = $"cuh{userId}", EmailHash = $"ceh{userId}", PasswordHash = "x",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    [Fact]
    public async Task A_consumer_cannot_see_the_order_under_its_own_rls_context()
    {
        var orderId = 810001L;
        await SeedPendingOrderAsync(orderId, projectId: 820001L, amountMinor: 500);

        await using var connection = await OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        // A consumer: app_user role, identity set to a plain user id that is
        // NOT a member of the order's project. This is what CreatePaymentIntent
        // used to run as — hence the 404 before the fix.
        await ExecAsync(connection, tx, "SET LOCAL ROLE app_user;");
        await ExecAsync(connection, tx,
            "SELECT set_config('app.current_user_id', '900001', true);");

        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM orders WHERE id = @id;", connection, tx);
        count.Parameters.AddWithValue("id", orderId);
        Assert.Equal(0, Convert.ToInt32(await count.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task The_worker_role_can_fulfill_the_order_atomically()
    {
        var orderId = 810002L;
        var userId = 900002L;
        await SeedPendingOrderAsync(orderId, projectId: 820002L, amountMinor: 750);
        await SeedUserAsync(userId); // ledger/notification FK a real users row

        await using var connection = await OpenAsync();
        await using (var tx = await connection.BeginTransactionAsync())
        {
            // The fulfillment posture: internal worker role inside a
            // transaction (SET LOCAL ROLE requires one). The worker-bypass
            // policies allow the order UPDATE, ledger INSERT and notification
            // INSERT that the consumer/anonymous roles cannot perform.
            await ExecAsync(connection, tx, "SET LOCAL ROLE app_internal_worker;");
            await ExecAsync(connection, tx,
                "SELECT set_config('app.current_user_id', '', true);");

            await ExecAsync(connection, tx,
                "UPDATE orders SET status = 'paid', paid_by_user_id = @uid, " +
                "stripe_payment_intent_id = 'pi_rls', paid_at = now() " +
                "WHERE id = @id AND status = 'pending';",
                ("uid", userId), ("id", orderId));
            await ExecAsync(connection, tx,
                "INSERT INTO ledger (id, user_id, order_id, stripe_payment_intent_id, " +
                "amount_minor, currency, created_at) " +
                "VALUES (830002, @uid, @id, 'pi_rls', 750, 'EUR', now());",
                ("uid", userId), ("id", orderId));
            await ExecAsync(connection, tx,
                "INSERT INTO user_notifications (id, user_id, type, title, body, created_at) " +
                "VALUES (840002, @uid, 'payment_completed', 'Payment completed', 'ok', now());",
                ("uid", userId));

            await tx.CommitAsync();
        }

        // Verify (superuser) that all three writes landed.
        await using var db = _factory.NewDbContext();
        var order = await db.Orders.FindAsync(orderId);
        Assert.Equal(Order.Statuses.Paid, order!.Status);
        Assert.Contains(db.Ledger, l => l.OrderId == orderId);
        Assert.Contains(db.UserNotifications, n => n.UserId == userId && n.Type == "payment_completed");
    }

    private static async Task ExecAsync(
        NpgsqlConnection connection, System.Data.Common.DbTransaction tx,
        string sql, params (string Name, object Value)[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, connection, (NpgsqlTransaction)tx);
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        await cmd.ExecuteNonQueryAsync();
    }
}
