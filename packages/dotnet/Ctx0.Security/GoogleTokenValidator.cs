using Ctx0.Security.Abstractions;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;

namespace Ctx0.Security;

/// <summary>
/// Validates Google ID tokens against Google's public keys
/// (AUTHENTICATION.md — Google OAuth). The allowed client ids come from
/// Authentication:Google:ClientIds. The validator fails closed: with no
/// configured client ids it rejects every token rather than accepting a
/// token minted for any other OAuth client (audience confusion).
/// </summary>
public sealed class GoogleTokenValidator(IConfiguration configuration) : IGoogleTokenValidator
{
    public async Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct)
    {
        var clientIds = configuration.GetSection("Authentication:Google:ClientIds")
            .Get<string[]>() ?? [];
        if (clientIds.Length == 0)
        {
            // Fail closed, loudly: an empty audience list would tell
            // GoogleJsonWebSignature to skip audience validation entirely.
            // This is a deployment misconfiguration, not a client error.
            throw new InvalidOperationException(
                "Google sign-in is enabled but Authentication:Google:ClientIds is empty. " +
                "Configure the OAuth client id(s) (docs/security/AUTHENTICATION.md).");
        }
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = clientIds,
                });
            return new GoogleUserInfo(payload.Subject, payload.Email, payload.Name);
        }
        catch (InvalidJwtException)
        {
            throw new CtxAuthenticationException("Invalid credentials provided.");
        }
    }
}
