using System.IO.Compression;
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
/// Exercises the data-subject rights end to end against a real PostgreSQL: an
/// export is built and bundled, the download token works exactly once, erasure
/// removes the account's rows while leaving other users untouched, and the consent
/// trail is per-user. The app connects as a non-superuser role so FORCE ROW LEVEL
/// SECURITY actually applies to it.
///
/// These assertions stay on what this feature and its `auth` dependency own, so
/// they hold whichever other features a workspace enables. The contributors those
/// features register widen the bundle's sections automatically — each one is
/// covered by its own feature's tests.
/// </summary>
public class GdprPrivacyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ctxapp")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminConnectionString = null!;
    private string _exportRoot = null!;

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

        _exportRoot = Path.Combine(Path.GetTempPath(), $"ctx-exports-{Guid.NewGuid():N}");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            TestConfig.Apply(builder);
            builder.UseEnvironment("Development"); // creates schema + RLS policies at startup
            builder.UseSetting("CONNECTION_STRINGS_DEFAULT", appConnectionString);
            builder.UseSetting("GDPR_EXPORT_ROOT", _exportRoot);
            builder.UseSetting("GDPR_POLICY_VERSION", "2024-11-01");
        });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _db.DisposeAsync();
        if (Directory.Exists(_exportRoot))
        {
            Directory.Delete(_exportRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Export_bundles_the_users_data_and_the_token_works_once()
    {
        var (client, email) = await NewUser();
        await Consent(client, new[] { "analytics" });

        var (jobId, token) = await RequestExport(client);
        await WaitForReady(client, jobId);

        // The archive on disk is ciphertext, not a readable zip.
        var stored = Directory.GetFiles(_exportRoot).Single();
        var raw = await File.ReadAllTextAsync(stored);
        Assert.DoesNotContain(email, raw);
        Assert.Equal(5, raw.Split('.').Length); // envelope format

        var download = await client.GetAsync($"/v1/privacy/export/{jobId}/download?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);

        using var archive = new ZipArchive(await download.Content.ReadAsStreamAsync());
        var manifest = archive.GetEntry("export.json");
        Assert.NotNull(manifest);
        using var reader = new StreamReader(manifest!.Open());
        var bundle = JsonDocument.Parse(await reader.ReadToEndAsync()).RootElement;

        // One section per registered contributor; `account` comes from `auth`,
        // which this feature requires, so it is always there.
        var sections = bundle.GetProperty("Sections");
        var account = sections.GetProperty("account");
        Assert.Equal(email, account.GetProperty("Email").GetString());
        Assert.True(account.GetProperty("HasPassword").GetBoolean());
        Assert.Single(account.GetProperty("Sessions").EnumerateArray());

        // Single use: the archive is gone and the token no longer works.
        Assert.Empty(Directory.GetFiles(_exportRoot));
        var replay = await client.GetAsync($"/v1/privacy/export/{jobId}/download?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.Gone, replay.StatusCode);
    }

    [Fact]
    public async Task Download_rejects_a_wrong_token()
    {
        var (client, _) = await NewUser();
        var (jobId, _) = await RequestExport(client);
        await WaitForReady(client, jobId);

        var response = await client.GetAsync($"/v1/privacy/export/{jobId}/download?token=not-the-token");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_an_account_erases_its_rows_everywhere_and_leaves_others_alone()
    {
        var (leaving, leavingEmail) = await NewUser();
        var (staying, stayingEmail) = await NewUser();

        await Consent(leaving, new[] { "analytics" });
        await Consent(staying, new[] { "analytics" });

        var wrongPassword = await leaving.PostAsJsonAsync(
            "/v1/privacy/account/delete", new { password = "not-my-password", confirm = "DELETE" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var unconfirmed = await leaving.PostAsJsonAsync(
            "/v1/privacy/account/delete", new { password = Password, confirm = "yes" });
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmed.StatusCode);

        var deleted = await leaving.PostAsJsonAsync(
            "/v1/privacy/account/delete", new { password = Password, confirm = "DELETE" });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        // Read as superuser, bypassing RLS: only the leaving user's rows are gone.
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(0, await CountFor(admin, "SELECT count(*) FROM \"Users\" WHERE \"Email\" = @email", leavingEmail));
        Assert.Equal(1, await CountFor(admin, "SELECT count(*) FROM \"Users\" WHERE \"Email\" = @email", stayingEmail));

        // Exactly the staying user's rows remain — including the ones the auth
        // contributor is responsible for, which is what ends the deleted session.
        Assert.Equal(1, await Count(admin, "SELECT count(*) FROM consent_records"));
        Assert.Equal(1, await Count(admin, "SELECT count(*) FROM user_credentials"));
        Assert.Equal(1, await Count(admin, "SELECT count(*) FROM refresh_tokens"));

        // The staying user is unaffected.
        var stillThere = await staying.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);

        // The deleted user's access token no longer identifies anyone.
        var gone = await leaving.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, gone.StatusCode);
    }

    [Fact]
    public async Task Consent_is_appended_per_user_and_reports_the_notice_in_force()
    {
        var (client, _) = await NewUser();

        var initial = await client.GetFromJsonAsync<JsonElement>("/v1/privacy/consent");
        Assert.Equal("2024-11-01", initial.GetProperty("policyVersion").GetString());
        Assert.Equal(JsonValueKind.Null, initial.GetProperty("consent").ValueKind);

        await Consent(client, new[] { "analytics", "marketing" });
        await Consent(client, Array.Empty<string>()); // withdrawal

        var current = await client.GetFromJsonAsync<JsonElement>("/v1/privacy/consent");
        Assert.Empty(current.GetProperty("consent").GetProperty("purposes").EnumerateArray());

        // Both decisions are kept: the trail is the evidence, not just the latest state.
        var (other, _) = await NewUser();
        var otherConsent = await other.GetFromJsonAsync<JsonElement>("/v1/privacy/consent");
        Assert.Equal(JsonValueKind.Null, otherConsent.GetProperty("consent").ValueKind);

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(2, await Count(admin, "SELECT count(*) FROM consent_records"));
    }

    private const string Password = "hunter2!pass";

    private async Task<(HttpClient Client, string Email)> NewUser()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var register = await _client.PostAsJsonAsync("/v1/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var tokens = await register.Content.ReadFromJsonAsync<JsonElement>();
        var access = tokens.GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return (client, email);
    }

    private static async Task Consent(HttpClient client, string[] purposes)
    {
        var response = await client.PutAsJsonAsync(
            "/v1/privacy/consent", new { policyVersion = "2024-11-01", purposes, source = "app" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<(string JobId, string Token)> RequestExport(HttpClient client)
    {
        var response = await client.PostAsync("/v1/privacy/export", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("jobId").GetString()!, json.GetProperty("downloadToken").GetString()!);
    }

    /// <summary>Poll the job the way the app does, until the background runner finishes it.</summary>
    private static async Task WaitForReady(HttpClient client, string jobId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var job = await client.GetFromJsonAsync<JsonElement>($"/v1/privacy/export/{jobId}");
            var status = job.GetProperty("status").GetString();
            if (status == "Ready")
            {
                return;
            }
            Assert.Equal("Pending", status); // never Failed
            await Task.Delay(100);
        }
        Assert.Fail("The export never became ready.");
    }

    private static async Task<long> Count(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long> CountFor(NpgsqlConnection connection, string sql, string email)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("email", email);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static Task Exec(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        return command.ExecuteNonQueryAsync();
    }
}
