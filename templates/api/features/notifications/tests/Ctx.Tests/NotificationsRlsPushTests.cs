using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CtxApp.Application.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CtxApp.Tests;

/// <summary>
/// Verifies notification RLS isolation, envelope encryption at rest, unread/read
/// transitions, device-token registration, and push fan-out — against a real
/// PostgreSQL instance with the app connecting as a non-superuser role so
/// FORCE ROW LEVEL SECURITY applies. Push delivery is captured by a fake
/// <see cref="IPushSender"/> so no external FCM setup is needed.
/// </summary>
public class NotificationsRlsPushTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ctxapp")
        .Build();

    private readonly CapturingPushSender _push = new();
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
            builder.UseSetting("ConnectionStrings:Default", appConnectionString);
            builder.ConfigureTestServices(services => services.AddSingleton<IPushSender>(_push));
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
    public async Task Rls_isolates_notifications_and_bodies_are_encrypted_at_rest()
    {
        var (aClient, _) = await NewUser();
        var (bClient, _) = await NewUser();

        await Create(aClient, "Alpha", "alpha-body");
        await Create(bClient, "Bravo", "bravo-body");

        // RLS: each user sees only their own notification.
        var aItems = await List(aClient);
        Assert.Single(aItems);
        Assert.Equal("Alpha", aItems[0].GetProperty("title").GetString());

        var bItems = await List(bClient);
        Assert.Single(bItems);
        Assert.Equal("Bravo", bItems[0].GetProperty("title").GetString());

        // At rest (read as superuser to bypass RLS): titles/bodies are ciphertext.
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Title\", \"Body\" FROM notifications", admin);
        await using var reader = await command.ExecuteReaderAsync();
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

    [Fact]
    public async Task Unread_count_tracks_reads()
    {
        var (client, _) = await NewUser();
        await Create(client, "One", "first");
        await Create(client, "Two", "second");

        Assert.Equal(2, await UnreadCount(client));

        var items = await List(client);
        var id = items[0].GetProperty("id").GetString()!;
        var read = await client.PostAsync($"/v1/notifications/{id}/read", content: null);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        Assert.Equal(1, await UnreadCount(client));
    }

    [Fact]
    public async Task Registered_device_receives_push_on_create()
    {
        var (client, _) = await NewUser();

        var register = await client.PostAsJsonAsync("/v1/notifications/devices", new { platform = "android", token = "fcm-token-abc" });
        Assert.Equal(HttpStatusCode.NoContent, register.StatusCode);

        await Create(client, "Hello", "world");

        Assert.Contains("fcm-token-abc", _push.Delivered);

        // Token is encrypted at rest.
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"Token\" FROM device_tokens", admin);
        var stored = (string)(await command.ExecuteScalarAsync())!;
        Assert.DoesNotContain("fcm-token-abc", stored);
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

    private static async Task Create(HttpClient client, string title, string body)
    {
        var response = await client.PostAsJsonAsync("/v1/notifications/", new { title, body });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<List<JsonElement>> List(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<JsonElement>("/v1/notifications/");
        return payload.GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task<int> UnreadCount(HttpClient client)
    {
        var payload = await client.GetFromJsonAsync<JsonElement>("/v1/notifications/unread-count");
        return payload.GetProperty("count").GetInt32();
    }

    private static Task Exec(NpgsqlConnection connection, string sql)
    {
        using var command = new NpgsqlCommand(sql, connection);
        return command.ExecuteNonQueryAsync();
    }

    /// <summary>Records the tokens each push targeted, so fan-out is assertable offline.</summary>
    private sealed class CapturingPushSender : IPushSender
    {
        public ConcurrentBag<string> Delivered { get; } = new();

        public Task SendAsync(IReadOnlyList<string> tokens, string title, string body, CancellationToken cancellationToken = default)
        {
            foreach (var token in tokens)
            {
                Delivered.Add(token);
            }
            return Task.CompletedTask;
        }
    }
}
