using System.Net.Http.Json;
using Contracts.Auth;
using Contracts.Items;
using NetTopologySuite.Geometries;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// The items-nearby output cache must actually store responses (M11). The
/// endpoint requires authorization, so without excludeDefaultPolicy the
/// built-in default would refuse to cache anything. This drives two
/// identical requests around a DB mutation and asserts the second is
/// served from the 30s cache (the newly inserted item is not yet visible).
/// </summary>
public sealed class OutputCacheTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task Identical_nearby_requests_are_served_from_cache()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await GetTokenAsync(client));

        // A query point far from every other test's fixtures so the cache
        // key (lat/lng/radiusKm) is unique to this test.
        const string url = "/v1/items/nearby?lat=40.0000&lng=40.0000&radiusKm=10";

        var first = await client.GetAsync(url);
        first.EnsureSuccessStatusCode();
        var before = await first.Content.ReadFromJsonAsync<List<ItemResponse>>();
        Assert.DoesNotContain(before!, i => i.Name == "CacheProbe");

        // Insert an item squarely inside the radius AFTER the first response
        // was cached.
        await using (var db = _factory.NewDbContext())
        {
            db.Items.Add(new Domain.Entities.Item
            {
                Id = 850001, Name = "CacheProbe",
                Location = new Point(40.0000, 40.0000) { SRID = 4326 },
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Same query within 30s → the cached (stale) response is returned,
        // so the just-inserted item is absent. If caching were inert this
        // would contain CacheProbe.
        var second = await client.GetAsync(url);
        second.EnsureSuccessStatusCode();
        var after = await second.Content.ReadFromJsonAsync<List<ItemResponse>>();
        Assert.DoesNotContain(after!, i => i.Name == "CacheProbe");
    }

    private async Task<string> GetTokenAsync(HttpClient client)
    {
        var email = $"cache-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/v1/users/register/send-code",
            new SendSignupCodeRequest(email));
        await using var db = _factory.NewDbContext();
        var verification = db.SignupVerifications.OrderByDescending(v => v.CreatedAt).First();
        var blindIndex = (IBlindIndexProvider)
            _factory.Services.GetService(typeof(IBlindIndexProvider))!;
        var code = Enumerable.Range(100000, 900000)
            .First(c => blindIndex.ComputeHash(c.ToString()) == verification.CodeHash).ToString();
        var reg = await client.PostAsJsonAsync("/v1/users",
            new RegisterUserRequest("ca_" + Guid.NewGuid().ToString("N")[..8],
                email, "s3cur3P@ss", code, null, null));
        return (await reg.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }
}
