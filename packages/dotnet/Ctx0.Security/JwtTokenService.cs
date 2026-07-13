using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ctx0.Security.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ctx0.Security;

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
            new(CtxClaimTypes.UserId, subject.UserId.ToString()),
            new(ClaimTypes.Role, subject.Role),
        };
        if (subject.Type is not null)
        {
            claims.Add(new Claim(CtxClaimTypes.UserType, subject.Type));
        }
        foreach (var orgId in subject.OrgIds ?? [])
        {
            claims.Add(new Claim(CtxClaimTypes.OrgId, orgId.ToString()));
        }
        if (subject.ProjectId is not null)
        {
            claims.Add(new Claim(
                CtxClaimTypes.ProjectId, subject.ProjectId.Value.ToString()));
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
