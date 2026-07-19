using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Acme.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Acme.Infrastructure.Security;

/// <summary>Resolves the authenticated principal from the validated JWT on the request.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var value = principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => UserId is not null;
}
