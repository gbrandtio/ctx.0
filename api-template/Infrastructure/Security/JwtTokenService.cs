using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Domain.Constants;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SharedKernel.Clock;

namespace Infrastructure.Security;

/// <summary>JWT configuration (AUTHENTICATION.md — Configuration).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "app-api";
    public string Audience { get; set; } = "app-mobile-client";

    /// <summary>HS256 key, ≥ 32 chars, injected via JWT_SIGNING_KEY.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

/// <summary>
/// HS256 access tokens + opaque refresh tokens (AUTHENTICATION.md).
/// Refresh tokens are 64 random bytes; only their SHA-256 hex hash is
/// ever persisted.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(AccessTokenSubject subject)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, subject.Username),
            new(SecurityConstants.ClaimTypes.UserId, subject.UserId.ToString()),
            new(ClaimTypes.Role, subject.Role),
        };
        if (subject.Type is not null)
        {
            claims.Add(new Claim(SecurityConstants.ClaimTypes.UserType, subject.Type));
        }
        foreach (var orgId in subject.OrgIds ?? [])
        {
            claims.Add(new Claim(SecurityConstants.ClaimTypes.OrgId, orgId.ToString()));
        }
        if (subject.ProjectId is not null)
        {
            claims.Add(new Claim(
                SecurityConstants.ClaimTypes.ProjectId, subject.ProjectId.Value.ToString()));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
