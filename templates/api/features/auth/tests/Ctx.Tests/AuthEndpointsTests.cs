using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CtxApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CtxApp.Tests;

/// <summary>A test host that swaps PostgreSQL for a shared in-memory SQLite database.</summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        TestConfig.Apply(builder);

        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(CtxAppDbContext)
                    || (d.ServiceType.FullName?.Contains("DbContextOptions") ?? false))
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();
            services.AddSingleton<DbConnection>(connection);

            // Isolate SQLite's EF services so they do not clash with the app's Npgsql provider.
            var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
            services.AddDbContext<CtxAppDbContext>((sp, options) =>
                options.UseSqlite(sp.GetRequiredService<DbConnection>()).UseInternalServiceProvider(efServiceProvider));
        });
    }

    public HttpClient CreateReadyClient()
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CtxAppDbContext>().Database.EnsureCreated();
        return client;
    }
}

public class AuthEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private static async Task<(string Access, string Refresh)> Tokens(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    [Fact]
    public async Task Register_login_refresh_and_me_all_work()
    {
        var client = factory.CreateReadyClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/v1/auth/register", new { email, password = "hunter2!pass" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var (access, refresh) = await Tokens(register);

        var login = await client.PostAsJsonAsync("/v1/auth/login", new { email, password = "hunter2!pass" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var refreshed = await client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var (_, rotated) = await Tokens(refreshed);
        Assert.NotEqual(refresh, rotated);

        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/v1/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var me = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var meBody = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, meBody.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_is_rejected()
    {
        var client = factory.CreateReadyClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/v1/auth/register", new { email, password = "hunter2!pass" });
        var (_, refresh) = await Tokens(register);

        // First rotation succeeds.
        var first = await client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Replaying the original (now-rotated) token is detected and rejected.
        var replay = await client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The rotation family is revoked, so the token from the first rotation is dead too.
        var (_, rotated) = await Tokens(first);
        var afterRevoke = await client.PostAsJsonAsync("/v1/auth/refresh", new { refreshToken = rotated });
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var client = factory.CreateReadyClient();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/v1/auth/register", new { email, password = "hunter2!pass" });

        var login = await client.PostAsJsonAsync("/v1/auth/login", new { email, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Me_without_a_token_is_unauthorized()
    {
        var client = factory.CreateReadyClient();
        var me = await client.GetAsync("/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
