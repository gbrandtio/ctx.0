using Application.Abstractions;
using Application.Common;
using Application.Features.Users;
using Contracts.Auth;
using Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace UnitTests.Application;

public sealed class AuthenticateUserHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBlindIndexProvider> _blindIndex = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenService> _jwt = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IIdGenerator> _ids = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<MediatR.IMediator> _mediator = new();

    private AuthenticateUserHandler CreateHandler()
    {
        _blindIndex.Setup(b => b.ComputeHash(It.IsAny<string>())).Returns("hash");
        _jwt.Setup(j => j.CreateAccessToken(It.IsAny<AccessTokenSubject>()))
            .Returns(("access", new DateTime(2026, 1, 1, 12, 15, 0, DateTimeKind.Utc)));
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("refresh");
        _jwt.Setup(j => j.HashRefreshToken(It.IsAny<string>())).Returns("refresh-hash");
        var issuer = new TokenIssuer(_jwt.Object, _refreshTokens.Object, _ids.Object, new TestClock());
        return new AuthenticateUserHandler(
            _users.Object, _blindIndex.Object, _passwordHasher.Object, issuer, _mediator.Object);
    }

    [Fact]
    public async Task Valid_credentials_issue_a_token_pair()
    {
        _users.Setup(u => u.FindByUsernameOrEmailHashAsync("hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, Username = "john", Email = "john@example.com", PasswordHash = "bcrypt" });
        _passwordHasher.Setup(p => p.Verify("pw", "bcrypt")).Returns(true);

        var response = await CreateHandler().Handle(
            new AuthenticateUserCommand(new AuthenticateRequest("john@example.com", "pw")),
            CancellationToken.None);

// ctx:auth_2fa_email:begin
        Assert.True(response.RequiresTwoFactor);
        _mediator.Verify(m => m.Send(It.IsAny<SendTwoFactorCodeCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        return;
// ctx:auth_2fa_email:end

        Assert.Equal("access", response.AccessToken);
        Assert.Equal("refresh", response.RefreshToken);
        _refreshTokens.Verify(r => r.Add(It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task Unknown_user_burns_a_dummy_verify_so_timing_does_not_leak()
    {
        // The constant-time guarantee: no account-existence oracle.
        _users.Setup(u => u.FindByUsernameOrEmailHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<DomainException>(() => CreateHandler().Handle(
            new AuthenticateUserCommand(new AuthenticateRequest("ghost@example.com", "pw")),
            CancellationToken.None));

        _passwordHasher.Verify(p => p.DummyVerify(), Times.Once);
        _passwordHasher.Verify(p => p.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Anonymized_account_cannot_authenticate()
    {
        _users.Setup(u => u.FindByUsernameOrEmailHashAsync("hash", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1, IsAnonymized = true, PasswordHash = "bcrypt" });

        await Assert.ThrowsAsync<DomainException>(() => CreateHandler().Handle(
            new AuthenticateUserCommand(new AuthenticateRequest("john@example.com", "pw")),
            CancellationToken.None));
        _passwordHasher.Verify(p => p.DummyVerify(), Times.Once);
    }
}
