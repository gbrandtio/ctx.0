using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CtxApp.Infrastructure.Security.Rls;

/// <summary>
/// Applies the registered Row-Level Security policies at startup. In Development
/// it also creates the schema (EnsureCreated) so a generated app runs against a
/// fresh PostgreSQL with no manual migration step. In other environments it runs
/// only when <c>Ctx:Rls:ApplyOnStartup</c> is true (otherwise policies are
/// expected to be applied by a migration). No-ops when there are no policies or
/// the provider is not PostgreSQL, and never opens a connection when it has no
/// work to do.
/// </summary>
public sealed class RlsInitializer(
    IServiceProvider services,
    IEnumerable<RlsPolicy> policies,
    IHostEnvironment environment,
    IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var declared = policies.ToList();
        var isDevelopment = environment.IsDevelopment();
        var applyOnStartup = string.Equals(configuration["Ctx:Rls:ApplyOnStartup"], "true", StringComparison.OrdinalIgnoreCase);
        if (declared.Count == 0 || !(isDevelopment || applyOnStartup))
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CtxAppDbContext>();
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        if (isDevelopment)
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }
        foreach (var policy in declared)
        {
            await CtxRls.EnableAsync(dbContext.Database, policy.Table, policy.UserColumn, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
