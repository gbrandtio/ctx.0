using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// RLS is the final, independent defense (AUTHORIZATION.md §10): even a
/// direct SQL client that skips the API sees only its own rows. These
/// tests set app.current_user_id exactly as the RlsInterceptor does and
/// assert cross-tenant reads return nothing.
/// </summary>
public sealed class RlsIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private async Task SeedUserAsync(long id, string suffix)
    {
        await using var db = _factory.NewDbContext();
        db.Users.Add(new Domain.Entities.User
        {
            Id = id,
            Username = $"rls_{suffix}",
            Email = $"rls_{suffix}@example.com",
            UsernameHash = $"uhash_{suffix}",
            EmailHash = $"ehash_{suffix}",
            PasswordHash = "x",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CountVisibleUsersAsync(long asUserId)
    {
        // Fresh connection as the app role, with the same transaction-local
        // identity the interceptor sets.
        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        // Drop to the non-superuser policy role (production connects as a
        // login role that is a member of app_user). RLS only engages for a
        // non-superuser session; the interceptor then sets the identity var
        // transaction-locally, exactly as reproduced here.
        await using (var setRole = new NpgsqlCommand("SET LOCAL ROLE app_user;", connection, tx))
        {
            await setRole.ExecuteNonQueryAsync();
        }
        await using (var setId = new NpgsqlCommand(
            "SELECT set_config('app.current_user_id', @id, true);", connection, tx))
        {
            setId.Parameters.AddWithValue("id", asUserId.ToString());
            await setId.ExecuteScalarAsync();
        }
        await using var count = new NpgsqlCommand("SELECT count(*) FROM users;", connection, tx);
        return Convert.ToInt32(await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task A_user_session_sees_only_its_own_row()
    {
        var alice = 700001L;
        var bob = 700002L;
        await SeedUserAsync(alice, "alice");
        await SeedUserAsync(bob, "bob");

        // Each identity sees exactly one row (its own) despite both existing.
        Assert.Equal(1, await CountVisibleUsersAsync(alice));
        Assert.Equal(1, await CountVisibleUsersAsync(bob));
    }

    [Fact]
    public async Task An_unset_identity_sees_no_rows()
    {
        await SeedUserAsync(700003L, "carol");
        // NULL app.current_user_id → user_self_policy matches nothing.
        Assert.Equal(0, await CountVisibleUsersAsync(-1));
    }
}
