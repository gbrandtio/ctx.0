using CtxApp.Api.Localization;
using CtxApp.Api.Security;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
// ctx:anchor:usings

var builder = WebApplication.CreateBuilder(args);

// --- Security plane (vendored): ALE, signing, JWT, envelope encryption, RLS ---
builder.Services.AddCtxSecurity(builder.Configuration);

// --- Localization: answer in the caller's language (always on) ---
builder.Services.AddCtxLocalization();

// --- Persistence (EF Core code-first, PostgreSQL) with the RLS interceptor ---
builder.Services.AddDbContext<CtxAppDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration["CONNECTION_STRINGS_DEFAULT"])
        .AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));

builder.Services.AddScoped<CtxApp.Application.Abstractions.IUnitOfWork, CtxApp.Infrastructure.Persistence.UnitOfWork>();

// ctx:anchor:services

var app = builder.Build();

// Resolve the request culture from Accept-Language before anything renders text.
app.UseCtxLocalization();

app.UseCtxSecurity();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Always-on security endpoints: ALE key discovery + device key enrollment.
app.MapCtxSecurityEndpoints();

// ctx:anchor:endpoints

app.Run();

// Exposed for integration tests.
public partial class Program;
