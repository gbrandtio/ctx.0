using Application.Abstractions;
using Domain.Exceptions;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Security;

/// <summary>
/// Validates Google ID tokens against Google's public keys
/// (AUTHENTICATION.md — Google OAuth). The allowed client ids come from
/// Authentication:Google:ClientIds.
/// </summary>
public sealed class GoogleTokenValidator(IConfiguration configuration) : IGoogleTokenValidator
{
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct)
    {
        var clientIds = configuration.GetSection("Authentication:Google:ClientIds")
            .Get<string[]>() ?? [];
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = clientIds.Length > 0 ? clientIds : null,
                });
            return new GoogleUserInfo(payload.Subject, payload.Email, payload.Name);
        }
        catch (InvalidJwtException)
        {
            throw DomainException.Unauthorized("Invalid credentials provided.");
        }
    }
}
