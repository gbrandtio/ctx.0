using Application.Abstractions;
using Ctx0.Security.Abstractions;

namespace Infrastructure.Security;

/// <summary>
/// Adapts the app-instance registration store to the security plane's
/// IDeviceKeyStore so RequestSigningMiddleware (Ctx0.Security) can resolve
/// a device's signing public key without knowing the entity model.
/// </summary>
public sealed class AppInstanceDeviceKeyStore(IAppInstanceRepository appInstances)
    : IDeviceKeyStore
{
    public async Task<string?> FindPublicKeyAsync(string deviceId, CancellationToken ct) =>
        (await appInstances.FindByDeviceIdAsync(deviceId, ct))?.PublicKey;
}
