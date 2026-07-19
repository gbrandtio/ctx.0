using Acme.Api.Security;
using Acme.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
// ctx:anchor:usings
using Acme.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// --- Security plane (vendored): ALE, signing, JWT, envelope encryption, RLS ---
builder.Services.AddCtxSecurity(builder.Configuration);

// --- Persistence (EF Core code-first, PostgreSQL) with the RLS interceptor ---
builder.Services.AddDbContext<AcmeDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));

// ctx:anchor:services
builder.Services.AddSingleton(new Acme.Infrastructure.Security.Rls.RlsPolicy("notes", "UserId"));
builder.Services.AddScoped<Acme.Application.Abstractions.IRefreshTokenStore, Acme.Infrastructure.Persistence.EfRefreshTokenStore>();
builder.Services.AddScoped<Acme.Application.Security.RefreshTokenService>();

var app = builder.Build();

app.UseCtxSecurity();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Always-on security endpoints: ALE key discovery + device key enrollment.
app.MapCtxSecurityEndpoints();

// ctx:anchor:endpoints
app.MapNotesEndpoints();
app.MapAuthEndpoints();
app.MapPingEndpoints();

app.Run();

// Exposed for integration tests.
public partial class Program;
