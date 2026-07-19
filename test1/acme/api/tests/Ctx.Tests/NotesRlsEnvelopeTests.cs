using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Acme.Tests;

/// <summary>
/// Verifies Row-Level Security isolation, envelope encryption at rest, and blind
/// index search against a real PostgreSQL instance. The app connects as a
/// non-superuser role so FORCE ROW LEVEL SECURITY actually applies to it.
/// </summary>
public class NotesRlsEnvelopeTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("acme")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _adminConnectionString = _db.GetConnectionString();

        // Create a non-superuser role for the app so RLS is enforced against it.
        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await Exec(admin, "CREATE ROLE app_user LOGIN PASSWORD 'app_pw' NOSUPERUSER");
            await Exec(admin, "GRANT ALL ON SCHEMA public TO app_user");
            await Exec(admin, "GRANT ALL ON DATABASE acme TO app_user");
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
            builder.UseSetting("ConnectionStrings:Default", appConnectionString);
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
    public async Task Rls_isolates_notes_and_bodies_are_encrypted_at_rest()
    {
        var (aClient, _) = await NewUser();
        var (bClient, _) = await NewUser();

        await CreateNote(aClient, "Alpha", "alpha-body");
        await CreateNote(bClient, "Bravo", "bravo-body");

        // RLS: each user sees only their own note.
        var aNotes = await ListNotes(aClient);
        Assert.Single(aNotes);
        Assert.Equal("Alpha", aNotes[0].GetProperty("title").GetString());

        var bNotes = await ListNotes(bClient);
        Assert.Single(bNotes);
        Assert.Equal("Bravo", bNotes[0].GetProperty("title").GetString());

        // Blind index search: A finds its own title, not B's.
        Assert.Single(await Search(aClient, "Alpha"));
        Assert.Empty(await Search(aClient, "Bravo"));

        // At rest (read as superuser to bypass RLS): titles/bodies are ciphertext.
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT \"Title\", \"Body\" FROM notes", admin);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
            var title = reader.GetString(0);
            var body = reader.GetString(1);
            Assert.DoesNotContain("Alpha", title);
            Assert.DoesNotContain("Bravo", title);
            Assert.DoesNotContain("body", body);
            Assert.Equal(5, title.Split('.').Length); // envelope format
        }
        Assert.Equal(2, rows);
    }

    private async Task<(HttpClient Client, string Email)> NewUser()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var register = await _client.PostAsJsonAsync("/v1/auth/register", new { email, password = "hunter2!pass" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var tokens = await register.Content.ReadFromJsonAsync<JsonElement>();
        var access = tokens.GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return (client, email);
    }

    private async Task CreateNote(HttpClient client, string title, string body)
    {
        var response = await client.PostAsJsonAsync("/v1/notes/", new { title, body });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<List<JsonElement>> ListNotes(HttpClient client)
    {
        var notes = await client.GetFromJsonAsync<List<JsonElement>>("/v1/notes/");
        return notes!;
    }

    private static async Task<List<JsonElement>> Search(HttpClient client, string title)
    {
        var notes = await client.GetFromJsonAsync<List<JsonElement>>($"/v1/notes/search?title={Uri.EscapeDataString(title)}");
        return notes!;
    }

    private static Task Exec(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        return command.ExecuteNonQueryAsync();
    }
}
