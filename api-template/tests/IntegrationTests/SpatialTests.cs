using System.Net.Http.Json;
using Contracts.Auth;
using Contracts.Items;
using NetTopologySuite.Geometries;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// Nearby query over real PostGIS geography (SPATIAL_QUERIES.md):
/// distances are meters via ST_DWithin, and the radius filter actually
/// excludes far items.
/// </summary>
public sealed class SpatialTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task Nearby_returns_close_items_and_excludes_far_ones()
    {
        await using (var db = _factory.NewDbContext())
        {
            // ~1.5 km and ~600 km from the query point in Berlin.
            db.Items.Add(new Domain.Entities.Item
            {
                Id = 800001, Name = "Near",
                Location = new Point(13.4050, 52.5300) { SRID = 4326 },
                CreatedAt = DateTime.UtcNow,
            });
            db.Items.Add(new Domain.Entities.Item
            {
                Id = 800002, Name = "Far",
                Location = new Point(2.3522, 48.8566) { SRID = 4326 }, // Paris
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", await GetTokenAsync(client));

        var response = await client.GetAsync(
            "/v1/items/nearby?lat=52.5200&lng=13.4050&radiusKm=10");
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<ItemResponse>>();

        Assert.Contains(items!, i => i.Name == "Near");
        Assert.DoesNotContain(items!, i => i.Name == "Far");
        Assert.All(items!, i => Assert.True(i.DistanceMeters >= 0));
    }

    private async Task<string> GetTokenAsync(HttpClient client)
    {
        var email = $"spatial-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/v1/users/register/send-code",
            new SendSignupCodeRequest(email));
        await using var db = _factory.NewDbContext();
        var verification = db.SignupVerifications.OrderByDescending(v => v.CreatedAt).First();
        var blindIndex = (Application.Abstractions.IBlindIndexProvider)
            _factory.Services.GetService(typeof(Application.Abstractions.IBlindIndexProvider))!;
        var code = Enumerable.Range(100000, 900000)
            .First(c => blindIndex.ComputeHash(c.ToString()) == verification.CodeHash).ToString();
        var reg = await client.PostAsJsonAsync("/v1/users",
            new RegisterUserRequest("sp_" + Guid.NewGuid().ToString("N")[..8],
                email, "s3cur3P@ss", code, null, null));
        return (await reg.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }
}
