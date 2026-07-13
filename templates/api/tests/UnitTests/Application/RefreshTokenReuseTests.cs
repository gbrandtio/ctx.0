using Application.Abstractions;
using Application.Common;
using Application.Features.Users;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace UnitTests.Application;

public sealed class RefreshTokenReuseTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Mock<IIdGenerator> _ids = new();
    private readonly TestClock _clock = new();

    private RefreshUserTokenHandler CreateHandler()
    {
        _jwt.Setup(j => j.HashRefreshToken(It.IsAny<string>())).Returns("hash");
        _jwt.Setup(j => j.CreateAccessToken(It.IsAny<AccessTokenSubject>()))
            .Returns(("access", _clock.UtcNow.AddMinutes(15)));
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh");
        var issuer = new TokenIssuer(_jwt.Object, _refreshTokens.Object, _ids.Object, _clock);
        return new RefreshUserTokenHandler(
            _refreshTokens.Object, _users.Object, _jwt.Object, issuer, _clock);
    }

    [Fact]
    public async Task Replaying_a_revoked_token_revokes_the_entire_family()
    {
        var familyId = Guid.NewGuid();
        _refreshTokens.Setup(r => r.FindByHashAsync("hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = 1, UserId = 1, UserType = SecurityConstants.Roles.User,
                FamilyId = familyId, IsRevoked = true, // already rotated
                ExpiresAt = _clock.UtcNow.AddDays(1),
            });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateHandler().Handle(new RefreshUserTokenCommand("stolen"), CancellationToken.None));

        Assert.Contains("reuse detected", ex.Message);
        _refreshTokens.Verify(r => r.RevokeFamilyAsync(familyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Valid_token_rotates_within_the_same_family()
    {
        var familyId = Guid.NewGuid();
        _refreshTokens.Setup(r => r.FindByHashAsync("hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = 5, UserId = 1, UserType = SecurityConstants.Roles.User,
                FamilyId = familyId, IsRevoked = false,
                ExpiresAt = _clock.UtcNow.AddDays(1),
            });
        _users.Setup(u => u.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "john", Email = "john@example.com" });

        var response = await CreateHandler().Handle(
            new RefreshUserTokenCommand("valid"), CancellationToken.None);

        Assert.Equal("new-refresh", response.RefreshToken);
        _refreshTokens.Verify(r => r.RevokeAsync(5, It.IsAny<CancellationToken>()), Times.Once);
        // The new token keeps the original family id.
        _refreshTokens.Verify(r => r.Add(It.Is<RefreshToken>(t => t.FamilyId == familyId)), Times.Once);
    }
}
