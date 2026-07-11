using System.Net;
using System.Net.Http.Json;
using Contracts.Auth;
using Xunit;

namespace IntegrationTests;

/// <summary>
/// End-to-end auth against real Postgres: register → authenticate → self
/// access, plus the IDOR and reuse-detection guarantees.
/// </summary>
public sealed class AuthFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    private async Task<AuthResponse> RegisterAsync(HttpClient client, string email)
    {
        await client.PostAsJsonAsync("/v1/users/register/send-code",
            new SendSignupCodeRequest(email));

        // The code is stored hashed; read it out of the DB for the test.
        await using var db = _factory.NewDbContext();
        var verification = db.SignupVerifications
            .OrderByDescending(v => v.CreatedAt).First();
        // Brute the 6-digit space against the stored hash via the same HMAC.
        var blindIndex = (Application.Abstractions.IBlindIndexProvider)
            _factory.Services.GetService(typeof(Application.Abstractions.IBlindIndexProvider))!;
        var code = Enumerable.Range(100000, 900000)
            .First(c => blindIndex.ComputeHash(c.ToString()) == verification.CodeHash)
            .ToString();

        var response = await client.PostAsJsonAsync("/v1/users",
            new RegisterUserRequest("user_" + Guid.NewGuid().ToString("N")[..8],
                email, "s3cur3P@ss", code, "Test User", null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task Register_then_authenticate_returns_a_working_session()
    {
        var client = _factory.CreateClient();
        var email = $"auth-{Guid.NewGuid():N}@example.com";
        var registered = await RegisterAsync(client, email);
        Assert.NotEmpty(registered.AccessToken);

        var auth = await client.PostAsJsonAsync("/v1/users/authenticate",
            new AuthenticateRequest(email, "s3cur3P@ss"));
        auth.EnsureSuccessStatusCode();
        var session = await auth.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new("Bearer", session!.AccessToken);
        var me = await client.GetAsync($"/v1/users/{session.UserId}");
        me.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        var client = _factory.CreateClient();
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/v1/users/authenticate",
            new AuthenticateRequest(email, "not-the-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Accessing_another_users_profile_is_forbidden()
    {
        var client = _factory.CreateClient();
        var session = await RegisterAsync(client, $"idor-{Guid.NewGuid():N}@example.com");
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);

        var response = await client.GetAsync($"/v1/users/{session.UserId + 1}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Replayed_refresh_token_revokes_the_family()
    {
        var client = _factory.CreateClient();
        var session = await RegisterAsync(client, $"reuse-{Guid.NewGuid():N}@example.com");

        var rotate = await client.PostAsJsonAsync("/v1/users/refresh",
            new RefreshRequest(session.RefreshToken));
        rotate.EnsureSuccessStatusCode();

        // Replaying the original (now-rotated) token is theft → 401.
        var replay = await client.PostAsJsonAsync("/v1/users/refresh",
            new RefreshRequest(session.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }
}
