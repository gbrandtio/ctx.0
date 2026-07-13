using Application.Abstractions;
using Application.Common;
using Application.Features.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.External;
using Infrastructure.Persistence;
using Infrastructure.Repositories.Auth;
using Infrastructure.Repositories.Items;
using Infrastructure.Repositories.Notifications;
using Infrastructure.Repositories.Orders;
using Infrastructure.Repositories.Users;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace AppApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Security options, crypto, identity, and RBAC services are wired
        // by AddCtxSecurity (SecurityExtensions.AddAppSecurity).
        // ctx:payments_stripe:begin
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        // ctx:payments_stripe:end

        // ---- Core singletons ----
        services.AddSingleton<EnvelopeEncryptionInterceptor>();

        // ---- Persistence (DATABASE_CODE_FIRST.md) ----
        services.AddScoped<RlsInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                    configuration.GetConnectionString("Default"),
                    npgsql => npgsql.UseNetTopologySuite())
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    sp.GetRequiredService<RlsInterceptor>(),
                    sp.GetRequiredService<EnvelopeEncryptionInterceptor>());
        });

        // ---- Identity & security adapters ----
        services.AddScoped<IDeviceKeyStore, AppInstanceDeviceKeyStore>();
        services.AddScoped<TokenIssuer>();

        // ---- Repositories ----
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGoogleIdentityRepository, GoogleIdentityRepository>();
        services.AddScoped<IFirebaseIdentityRepository, FirebaseIdentityRepository>();
        services.AddScoped<IUserExportRepository, UserExportRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ISignupVerificationRepository, SignupVerificationRepository>();
        services.AddScoped<IAppInstanceRepository, AppInstanceRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IItemRepository, ItemRepository>();

        // ---- External services ----
        // ctx:payments_stripe:begin
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        // ctx:payments_stripe:end
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
// ctx:email_brevo:begin
        services.Configure<BrevoOptions>(configuration.GetSection("Brevo"));
        services.AddHttpClient<IEmailSender, BrevoEmailSender>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BrevoOptions>>().Value;
            client.BaseAddress = new Uri("https://api.brevo.com/v3/");
            client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
        });
// ctx:email_brevo:end

        // ---- CQRS ----
        services.AddMediatR(config =>
            config.RegisterServicesFromAssemblyContaining<AuthenticateUserHandler>());

        // ---- Background workers ----
        // ctx:push_firebase:begin
        services.AddHostedService<PostgresNotificationListener>();
        // ctx:push_firebase:end
        services.AddHostedService<KekRotationWorker>();

        return services;
    }
}
