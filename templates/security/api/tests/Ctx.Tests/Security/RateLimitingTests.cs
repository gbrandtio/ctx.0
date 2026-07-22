using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CtxApp.Tests.Security;

/// <summary>Verifies the global rate limiter rejects requests past the configured limit.</summary>
public class RateLimitingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RateLimitingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production"); // no database needed for /health
            TestConfig.Apply(builder);
            builder.UseSetting("CTX_RATE_LIMIT_PERMIT_LIMIT", "3");
            builder.UseSetting("CTX_RATE_LIMIT_WINDOW_SECONDS", "60");
            builder.UseSetting("CONNECTION_STRINGS_DEFAULT", "Host=localhost;Database=ctxapp;Username=ctxapp;Password=x");
        });
    }

    [Fact]
    public async Task Requests_past_the_limit_get_429()
    {
        var client = _factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await client.GetAsync("/health");
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
