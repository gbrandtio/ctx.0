using CtxApp.Application.Abstractions;
using CtxApp.Application.Notifications;
using CtxApp.Infrastructure.Gdpr;
using CtxApp.Infrastructure.Security.Rls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CtxApp.Api.Notifications;

/// <summary>
/// Registration surface for the notifications feature. The base <c>Program.cs</c>
/// calls <see cref="AddCtxNotifications"/> during service configuration. It
/// registers per-user RLS isolation for both notification tables, declares the
/// personal data they hold, and binds <see cref="IPushSender"/> to real FCM
/// delivery when configured, or to the logging no-op otherwise.
/// </summary>
public static class NotificationsBootstrap
{
    public static IServiceCollection AddCtxNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        // Per-user row isolation, enforced by the security plane's RLS interceptor.
        services.AddSingleton(new RlsPolicy("notifications", "UserId"));
        services.AddSingleton(new RlsPolicy("device_tokens", "UserId"));

        // Personal data this feature holds, for the gdpr feature's export/erasure.
        services.AddScoped<IPersonalDataContributor, NotificationsPersonalData>();

        var fcm = FcmOptions.FromConfiguration(configuration);
        services.AddSingleton(fcm);

        if (!string.IsNullOrWhiteSpace(fcm.ProjectId) && !string.IsNullOrWhiteSpace(fcm.ServiceAccountJson))
        {
            services.AddSingleton<IPushSender, FcmPushSender>();
        }
        else
        {
            services.AddSingleton<IPushSender, LoggingPushSender>();
        }

        return services;
    }
}
