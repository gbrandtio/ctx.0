using CtxApp.Api.Localization;
using CtxApp.Api.Security;
using CtxApp.Application.Abstractions;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CtxApp.Api.Configuration;

/// <summary>
/// Service-registration section of the composition root. <c>Program.cs</c> calls
/// <see cref="AddCtxServices"/> to populate the dependency-injection container:
/// the vendored security plane, always-on localization, persistence, and whatever
/// enabled features wire in at the <c>services</c> anchor below.
/// </summary>
public static class ServiceRegistration
{
    public static WebApplicationBuilder AddCtxServices(this WebApplicationBuilder builder)
    {
        var settings = EnvironmentSettings.FromConfiguration(builder.Configuration);
        builder.Services.AddSingleton(settings);

        // --- Security plane (vendored): ALE, signing, JWT, envelope encryption, RLS ---
        builder.Services.AddCtxSecurity(builder.Configuration);

        // --- Localization: answer in the caller's language (always on) ---
        builder.Services.AddCtxLocalization();

        // --- Persistence (EF Core code-first, PostgreSQL) with the RLS interceptor ---
        builder.Services.AddDbContext<CtxAppDbContext>((sp, options) =>
            options
                .UseNpgsql(settings.ConnectionStringsDefault)
                .AddInterceptors(sp.GetServices<IInterceptor>()));

        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ctx:anchor:services

        return builder;
    }
}
