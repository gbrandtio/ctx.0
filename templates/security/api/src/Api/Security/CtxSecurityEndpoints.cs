using CtxApp.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CtxApp.Api.Security;

/// <summary>Enrollment record: a device and the base64 uncompressed P-256 public key it signs with.</summary>
public sealed record DeviceEnrollment(string DeviceId, string PublicKey);

/// <summary>Always-on security-plane endpoints: ALE key discovery and device key enrollment.</summary>
public static class CtxSecurityEndpoints
{
    public static IEndpointRouteBuilder MapCtxSecurityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/security");

        // Clients fetch the server's static ALE public key to seal request bodies.
        group.MapGet("/ale-public-key", (IAleKeyProvider keys) =>
            Results.Ok(new { publicKey = Convert.ToBase64String(keys.PublicKey) }));

        // A device enrolls the ECDSA public key its requests are signed with.
        group.MapPost("/devices", (DeviceEnrollment body, IDeviceKeyRegistry registry) =>
        {
            byte[] publicKey;
            try
            {
                publicKey = Convert.FromBase64String(body.PublicKey);
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { error = "publicKey must be base64." });
            }

            try
            {
                registry.Register(body.DeviceId, publicKey);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            return Results.NoContent();
        });

        return app;
    }
}
