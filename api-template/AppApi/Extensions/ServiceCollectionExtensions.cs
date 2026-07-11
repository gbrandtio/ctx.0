using Application.Abstractions;
using Application.Common;
using Application.Features.Users;
using Infrastructure.BackgroundJobs;
using Infrastructure.External;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Interceptors;
using Infrastructure.Repositories.Auth;
using Infrastructure.Repositories.Items;
using Infrastructure.Repositories.Notifications;
using Infrastructure.Repositories.Orders;
using Infrastructure.Repositories.Users;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Clock;

namespace AppApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Options (env-var secrets; validated at startup) ----
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32,
                "Jwt:SigningKey must be at least 32 characters (JWT_SIGNING_KEY).")
            .ValidateOnStart();
        services.AddOptions<EncryptionOptions>()
            .Bind(configuration.GetSection(EncryptionOptions.SectionName))
            .Validate(o => !string.IsNullOrEmpty(o.CurrentVersion) &&
                           o.Keys.ContainsKey(o.CurrentVersion),
                "Security:Encryption must define CurrentVersion and its key.")
            .ValidateOnStart();
        services.Configure<AleOptions>(configuration.GetSection(AleOptions.SectionName));
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));

        // ---- Core singletons ----
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IIdGenerator>(
            new SnowflakeIdGenerator(configuration.GetValue("NodeId", 0)));
        services.AddSingleton<AesEncryptionProvider>();
        services.AddSingleton<IBlindIndexProvider, BlindIndexProvider>();
        services.AddSingleton<EnvelopeEncryptionInterceptor>();
        services.AddSingleton<CurrentUserContext>();
        services.AddSingleton<ICurrentUserProvider>(
            sp => sp.GetRequiredService<CurrentUserContext>());
        services.AddSingleton(sp => new AleCryptoService(
            configuration[$"{AleOptions.SectionName}:RsaPrivateKey"]
                ?? throw new InvalidOperationException(
                    "Security:Ale:RsaPrivateKey is required.")));

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

        // ---- Identity & security services ----
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
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
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        // ---- CQRS ----
        services.AddMediatR(config =>
            config.RegisterServicesFromAssemblyContaining<AuthenticateUserHandler>());

        // ---- Background workers ----
        services.AddHostedService<PostgresNotificationListener>();
        services.AddHostedService<KekRotationWorker>();

        return services;
    }
}
