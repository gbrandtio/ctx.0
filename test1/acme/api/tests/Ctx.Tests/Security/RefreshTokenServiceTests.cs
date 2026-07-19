using Acme.Application.Abstractions;
using Acme.Application.Security;
using Acme.Domain.Security;
using Xunit;

namespace Acme.Tests.Security;

/// <summary>
/// Unit tests for refresh-token rotation and reuse detection, over an in-memory
/// store so the security logic is verified without a database.
/// </summary>
public class RefreshTokenServiceTests
{
    private sealed class FakeStore : IRefreshTokenStore
    {
        public readonly List<RefreshToken> Tokens = [];
        public Task AddAsync(RefreshToken token, CancellationToken ct = default) { Tokens.Add(token); return Task.CompletedTask; }
        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
            => Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));
        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken ct = default)
        {
            foreach (var t in Tokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)) t.RevokedAt = revokedAt;
            return Task.CompletedTask;
        }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeJwt : IJwtIssuer
    {
        public (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId) => ($"access.{userId}", DateTimeOffset.UtcNow.AddMinutes(15));
    }

    private sealed class SequentialGenerator : ITokenGenerator
    {
        private int _n;
        public string NewToken() => $"tok{++_n}";
    }

    private sealed class IdentityHasher : ITokenHasher
    {
        public string Hash(string token) => $"h:{token}";
    }

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private static (RefreshTokenService Service, FakeStore Store, MutableClock Clock) Build()
    {
        var store = new FakeStore();
        var clock = new MutableClock();
        var service = new RefreshTokenService(store, new FakeJwt(), new SequentialGenerator(), new IdentityHasher(), clock, new RefreshTokenTtl(TimeSpan.FromDays(1)));
        return (service, store, clock);
    }

    [Fact]
    public async Task Issue_then_rotate_returns_a_new_token_and_revokes_the_old()
    {
        var (service, store, _) = Build();
        var user = Guid.NewGuid();

        var first = await service.IssueAsync(user);
        Assert.Equal("tok1", first.RefreshToken);

        var second = await service.RotateAsync(first.RefreshToken);
        Assert.Equal("tok2", second.RefreshToken);

        var original = store.Tokens.Single(t => t.TokenHash == "h:tok1");
        Assert.NotNull(original.RevokedAt);
        Assert.NotNull(original.ReplacedByTokenId);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var (service, store, _) = Build();
        var first = await service.IssueAsync(Guid.NewGuid());
        await service.RotateAsync(first.RefreshToken); // tok1 -> tok2

        // Replay the already-rotated tok1.
        var ex = await Assert.ThrowsAsync<AuthException>(() => service.RotateAsync(first.RefreshToken));
        Assert.Contains("reuse", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Every token in the family is now revoked, including the live tok2.
        Assert.All(store.Tokens, t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task Rotating_an_expired_token_is_rejected()
    {
        var (service, store, clock) = Build();
        var first = await service.IssueAsync(Guid.NewGuid());

        clock.UtcNow = clock.UtcNow.AddDays(2);

        await Assert.ThrowsAsync<AuthException>(() => service.RotateAsync(first.RefreshToken));
        Assert.Single(store.Tokens); // no replacement issued
    }

    [Fact]
    public async Task Unknown_token_is_rejected()
    {
        var (service, _, _) = Build();
        await Assert.ThrowsAsync<AuthException>(() => service.RotateAsync("never-issued"));
    }
}
