using CtxApp.Application.Abstractions;
using CtxApp.Application.Gdpr;
using CtxApp.Infrastructure.Gdpr;
using CtxApp.Infrastructure.Security.Rls;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CtxApp.Api.Gdpr;

/// <summary>
/// Registration surface for the privacy feature. The base <c>Program.cs</c> calls
/// <see cref="AddCtxGdpr"/> during service configuration: it reads
/// <see cref="GdprOptions"/>, declares per-user RLS isolation for the consent and
/// export tables, and wires the archive store, exporter, eraser and background
/// export runner.
/// </summary>
public static class GdprBootstrap
{
    public static IServiceCollection AddCtxGdpr(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new RlsPolicy("consent_records", "UserId"));
        services.AddSingleton(new RlsPolicy("data_export_jobs", "UserId"));

        var options = GdprOptions.FromConfiguration(configuration);
        services.AddSingleton(options);

        // Replaces the security plane's request-bound ICurrentUser with one that
        // also honours a background job's declared subject. Registered after
        // AddCtxSecurity (Program.cs calls that first), so this resolution wins.
        services.AddScoped<ICurrentUser, SubjectScopedCurrentUser>();

        services.AddSingleton<IExportArchiveStore, ExportArchiveStore>();
        services.AddScoped<PersonalDataExporter>();
        services.AddScoped<AccountEraser>();

        services.AddSingleton<IExportJobQueue, ExportJobQueue>();
        services.AddHostedService<ExportJobRunner>();

        services.AddScoped<IPrivacyRepository, CtxApp.Infrastructure.Persistence.PrivacyRepository>();
        services.AddScoped<IPrivacyService, PrivacyService>();

        return services;
    }
}
