using CtxApp.Api.Security;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
// ctx:anchor:usings

var builder = WebApplication.CreateBuilder(args);

// --- Security plane (vendored): ALE, signing, JWT, envelope encryption, RLS ---
builder.Services.AddCtxSecurity(builder.Configuration);

// --- Persistence (EF Core code-first, PostgreSQL) with the RLS interceptor ---
builder.Services.AddDbContext<CtxAppDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));

// ctx:anchor:services

var app = builder.Build();

app.UseCtxSecurity();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Always-on security endpoints: ALE key discovery + device key enrollment.
app.MapCtxSecurityEndpoints();

// ctx:anchor:endpoints

app.Run();

// Exposed for integration tests.
public partial class Program;
