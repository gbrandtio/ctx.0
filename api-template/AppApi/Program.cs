using AppApi.Endpoints;
using AppApi.Endpoints.v1;
using AppApi.Extensions;
using AppApi.Filters;
using AppApi.Middleware;
using AppApi.Realtime;
using Domain.Constants;
using Infrastructure.Security;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging: Serilog with console sink; PII is never logged. ----
builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddAppSecurity(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// SSE (ADR-0003).
builder.Services.AddSingleton<ProjectEventsBroadcaster>();
builder.Services.AddHostedService<PostgresPaymentUpdateListener>();

// Output caching: 30s for high-traffic reads (CACHING_STRATEGY.md).
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("items-nearby", policy => policy
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("lat", "lng", "radiusKm")
        .Tag("items"));
});

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation());

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();
app.UseRouting();

// Security pipeline order is contractual (APPLICATION_LAYER_SECURITY.md):
// decrypt first, then verify the signature against the plaintext.
app.UseMiddleware<AleMiddleware>();
app.UseMiddleware<RequestSigningMiddleware>();

app.UseAuthentication();

// RLS identity: expose the JWT uid to the RlsInterceptor for this
// request's async flow (AUTHORIZATION.md §11).
app.Use((context, next) =>
{
    var userContext = context.RequestServices.GetRequiredService<CurrentUserContext>();
    var uid = context.User.FindFirst(SecurityConstants.ClaimTypes.UserId)?.Value;
    userContext.SetUser(long.TryParse(uid, out var id) ? id : null);
    return next(context);
});

app.UseAuthorization();
app.UseRateLimiter();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // exported to mobile-template/docs/API/swagger.json
}
app.MapHealthChecks("/health").AllowAnonymous();

// ---- THE endpoint-module registration point (EXTENDING_THE_TEMPLATE.md §5).
// Adding a business feature = one IEndpointModule line here + its
// SecurityConstants entries. Group-level filters cover every module.
List<IEndpointModule> modules =
[
    new SecurityEndpoints(),
    new UsersEndpoints(),
    new OrdersEndpoints(),
    new PaymentsEndpoints(),
    new ItemsEndpoints(),
    new ProjectsEndpoints(),
];

// The global partitioned limiter already covers every endpoint; auth and
// registration endpoints opt into their stricter named policies.
var v1 = app.MapGroup("/v1")
    .AddEndpointFilter<SanitizationFilter>();
foreach (var module in modules)
{
    module.Map(v1);
}

app.Run();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
