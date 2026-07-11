using AppApi.Endpoints;
using AppApi.Middleware;
using Application.Abstractions;
using Contracts.Security;
using Domain.Entities;
using Infrastructure.Security;
using Microsoft.Extensions.Options;
using SharedKernel.Clock;

namespace AppApi.Endpoints.v1;

/// <summary>
/// Security discovery + app-instance registration
/// (APPLICATION_LAYER_SECURITY.md §2–3). Metadata is anonymous and
/// unsigned (it bootstraps the security posture); registration is
/// ALE-encrypted but unsigned (the device has no verified key yet).
/// </summary>
public sealed class SecurityEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder v1)
    {
        var group = v1.MapGroup("/security");

        group.MapGet("/metadata", (
                AleCryptoService ale,
                IOptions<AleOptions> options) =>
            Results.Ok(new SecurityMetadataResponse(
                AleEnabled: options.Value.Enforced,
                AlePublicKey: ale.PublicKeyPem,
                RequestSigningRequired: options.Value.RequestSigningRequired,
                SignatureWindowSeconds: options.Value.SignatureWindowSeconds,
                SupportedAttestationTypes: ["GooglePlayIntegrity", "AppleAppAttest"])))
            .WithMetadata(new AllowPlaintextAttribute())
            .WithMetadata(new SkipRequestSigningAttribute())
            .AllowAnonymous();

        group.MapPost("/app-instances", async (
                RegisterAppInstanceRequest request,
                IAppInstanceRepository appInstances,
                IIdGenerator ids,
                IClock clock,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.DeviceId) ||
                    string.IsNullOrWhiteSpace(request.PublicKey))
                {
                    return Results.BadRequest();
                }

                var existing = await appInstances.FindByDeviceIdAsync(request.DeviceId, ct);
                if (existing is not null)
                {
                    // Re-registration replaces the key (device identity reset).
                    existing.PublicKey = request.PublicKey;
                    existing.UpdatedAt = clock.UtcNow;
                }
                else
                {
                    appInstances.Add(new AppInstance
                    {
                        Id = ids.NextId(),
                        DeviceId = request.DeviceId,
                        PublicKey = request.PublicKey,
                        CreatedAt = clock.UtcNow,
                        UpdatedAt = clock.UtcNow,
                    });
                }
                await appInstances.SaveChangesAsync(ct);
                return Results.Created($"/v1/security/app-instances/{request.DeviceId}", null);
            })
            .WithMetadata(new SkipRequestSigningAttribute())
            .AllowAnonymous();
    }
}
