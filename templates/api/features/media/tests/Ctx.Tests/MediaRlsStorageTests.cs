using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CtxApp.Tests;

/// <summary>
/// Verifies media upload/download round-trips, Row-Level Security isolation
/// between users, and that both the file name (column) and the blob (on disk) are
/// ciphertext at rest. Runs against a real PostgreSQL, with the app connected as a
/// non-superuser role so FORCE ROW LEVEL SECURITY applies.
/// </summary>
public class MediaRlsStorageTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ctxapp")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminConnectionString = null!;
    private string _mediaRoot = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _adminConnectionString = _db.GetConnectionString();
        _mediaRoot = Path.Combine(Path.GetTempPath(), $"ctx-media-{Guid.NewGuid():N}");

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
            builder.UseSetting("ConnectionStrings:Default", appConnectionString);
            builder.UseSetting("Media:Root", _mediaRoot);
        });
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _db.DisposeAsync();
        if (Directory.Exists(_mediaRoot))
        {
            Directory.Delete(_mediaRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Rls_isolates_media_and_bytes_are_encrypted_at_rest()
    {
        var aClient = await NewUser();
        var bClient = await NewUser();

        var aId = await Upload(aClient, "alpha.txt", "text/plain", "alpha-secret-body");
        await Upload(bClient, "bravo.txt", "text/plain", "bravo-secret-body");

        // RLS: each user lists only their own object.
        var aItems = await ListMedia(aClient);
        Assert.Single(aItems);
        Assert.Equal("alpha.txt", aItems[0].GetProperty("fileName").GetString());

        var bItems = await ListMedia(bClient);
        Assert.Single(bItems);
        Assert.Equal("bravo.txt", bItems[0].GetProperty("fileName").GetString());

        // Download round-trips the exact bytes for the owner.
        var download = await aClient.GetAsync($"/v1/media/{aId}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("alpha-secret-body", await download.Content.ReadAsStringAsync());

        // RLS: user B cannot read user A's object.
        var cross = await bClient.GetAsync($"/v1/media/{aId}");
        Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        // At rest, the file-name column is ciphertext (envelope has 5 dotted parts).
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand("SELECT \"FileName\" FROM media", admin))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                Assert.DoesNotContain("alpha", name);
                Assert.DoesNotContain("bravo", name);
                Assert.Equal(5, name.Split('.').Length);
            }
        }

        // At rest, the blob files are ciphertext, not the plaintext body.
        var blobFiles = Directory.GetFiles(_mediaRoot);
        Assert.Equal(2, blobFiles.Length);
        foreach (var file in blobFiles)
        {
            var contents = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain("secret-body", contents);
            Assert.Equal(5, contents.Split('.').Length);
        }
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

    private static async Task<string> Upload(HttpClient client, string fileName, string contentType, string body)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);

        var response = await client.PostAsync("/v1/media/", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private static async Task<List<JsonElement>> ListMedia(HttpClient client)
    {
        var json = await client.GetFromJsonAsync<JsonElement>("/v1/media/");
        return json.GetProperty("items").EnumerateArray().ToList();
    }

    private static Task Exec(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        return command.ExecuteNonQueryAsync();
    }
}
