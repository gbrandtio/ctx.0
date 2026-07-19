using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CtxApp.Application.Abstractions;
using Microsoft.IdentityModel.Tokens;

namespace CtxApp.Infrastructure.Security.Jwt;

/// <summary>Issues HMAC-SHA256 signed JWT access tokens whose subject is the user id.</summary>
public sealed class JwtIssuer(JwtOptions options, IClock clock) : IJwtIssuer
{
    public (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(options.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
