using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CtxApp.Tests;

/// <summary>
/// Verifies profile upsert round-trips, Row-Level Security isolation between
/// users, and that the display name is envelope-encrypted at rest. Runs against a
/// real PostgreSQL with the app connected as a non-superuser role so FORCE ROW
/// LEVEL SECURITY applies.
/// </summary>
public class ProfileRlsTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ctxapp")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _adminConnectionString = _db.GetConnectionString();

        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await Exec(admin, "CREATE ROLE app_user LOGIN PASSWORD 'app_pw' NOSUPERUSER");
            await Exec(admin, "GRANT ALL ON SCHEMA public TO app_user");
            await Exec(admin, "GRANT ALL ON DATABASE ctxapp TO app_user");
        }

        var appConnectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Username = "app_user",
            Password = "app_pw",
        }.ConnectionString;

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            TestConfig.Apply(builder);
            builder.UseEnvironment("Development"); // creates schema + RLS policies at startup
            builder.UseSetting("CONNECTION_STRINGS_DEFAULT", appConnectionString);
        });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Profile_upserts_isolates_per_user_and_encrypts_display_name()
    {
        var aClient = await NewUser();
        var bClient = await NewUser();

        // First GET auto-creates an empty profile.
        var initial = await aClient.GetFromJsonAsync<JsonElement>("/v1/profile/");
        Assert.Equal(string.Empty, initial.GetProperty("displayName").GetString());

        // PUT upserts and round-trips.
        var put = await aClient.PutAsJsonAsync("/v1/profile/", new { displayName = "Ada Lovelace", bio = "Analytical." });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ada Lovelace", saved.GetProperty("displayName").GetString());

        var reread = await aClient.GetFromJsonAsync<JsonElement>("/v1/profile/");
        Assert.Equal("Ada Lovelace", reread.GetProperty("displayName").GetString());
        Assert.Equal("Analytical.", reread.GetProperty("bio").GetString());

        // RLS: user B sees its own (fresh, empty) profile, never A's.
        var bProfile = await bClient.GetFromJsonAsync<JsonElement>("/v1/profile/");
        Assert.Equal(string.Empty, bProfile.GetProperty("displayName").GetString());

        // At rest (read as superuser to bypass RLS): every display name is
        // ciphertext — even the empty-plaintext profile is a 5-part envelope, so
        // filter on that shape rather than on the (encrypted) value.
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"DisplayName\" FROM user_profiles", admin);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
            var name = reader.GetString(0);
            Assert.DoesNotContain("Ada", name);
            Assert.Equal(5, name.Split('.').Length); // envelope format
        }
        Assert.Equal(2, rows); // user A (Ada) + user B (auto-created empty)
    }

    private async Task<HttpClient> NewUser()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var register = await _client.PostAsJsonAsync("/v1/auth/register", new { email, password = "hunter2!pass" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var tokens = await register.Content.ReadFromJsonAsync<JsonElement>();
        var access = tokens.GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return client;
    }

    private static Task Exec(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        return command.ExecuteNonQueryAsync();
    }
}
