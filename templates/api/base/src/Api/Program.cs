using CtxApp.Api.Configuration;
// ctx:anchor:usings

var builder = WebApplication.CreateBuilder(args);

// Register services (security, localization, persistence, enabled features).
builder.AddCtxServices();

var app = builder.Build();

// Assemble the request pipeline, then map endpoints.
app.UseCtxPipeline();
app.MapCtxEndpoints();

app.Run();

// Exposed for integration tests.
public partial class Program;
