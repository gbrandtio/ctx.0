using AppApi.Endpoints;
using AppApi.Endpoints.v1;
using AppApi.Extensions;
using AppApi.Filters;
using AppApi.Middleware;
using AppApi.Realtime;
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

// SSE (ADR-0003). The broadcaster is generic; the payments listener is
// its only publisher today and toggles with payments_stripe.
builder.Services.AddSingleton<ProjectEventsBroadcaster>();
// ctx:payments_stripe:begin
builder.Services.AddHostedService<PostgresPaymentUpdateListener>();
// ctx:payments_stripe:end

// Output caching: 30s for high-traffic reads (CACHING_STRATEGY.md).
builder.Services.AddOutputCache(options =>
{
    // ctx:maps_google:begin
    options.AddPolicy("items-nearby", policy => policy
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByQuery("lat", "lng", "radiusKm")
        .Tag("items"));
    // ctx:maps_google:end
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

// The whole security pipeline (ALE → signing → authN → RLS identity →
// authZ → rate limiting) is wired by this single seam; its internal
// order is contractual (APPLICATION_LAYER_SECURITY.md).
app.UseAppSecurity();

// ctx:app_updates:begin
app.UseMiddleware<VersionCheckMiddleware>();
// ctx:app_updates:end


app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // exported to templates/mobile/docs/API/swagger.json
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
    // ctx:payments_stripe:begin
    new PaymentsEndpoints(),
    // ctx:payments_stripe:end
    // ctx:maps_google:begin
    new ItemsEndpoints(),
    // ctx:maps_google:end
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
