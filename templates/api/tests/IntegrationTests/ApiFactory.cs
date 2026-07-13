using System.Security.Cryptography;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway Postgres+PostGIS container,
/// applying the FULL migration set (DATABASE_CODE_FIRST.md §7) so RLS
/// policies and PostGIS behavior are exercised for real — never the
/// InMemory provider.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4")
        .WithDatabase("app")
        .WithUsername("app")
        .WithPassword("app")
        .Build();

    private string _rsaPrivateKeyPem = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-at-least-32-chars-long");
        builder.UseSetting("Security:Encryption:CurrentVersion", "v1");
        builder.UseSetting("Security:Encryption:Keys:v1:Key",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Security:Encryption:BlindIndexKey",
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Security:Ale:RsaPrivateKey", _rsaPrivateKeyPem);
        builder.UseSetting("Security:Ale:Enforced", "false");
        builder.UseSetting("Security:Ale:RequestSigningRequired", "false");
// ctx:app_updates:begin
        builder.UseSetting("MINIMUM_CLIENT_VERSION", "");
// ctx:app_updates:end
        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        using (var rsa = RSA.Create(2048))
        {
            _rsaPrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        }
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    public AppDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), o => o.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }

    public string ConnectionString => _postgres.GetConnectionString();
}
