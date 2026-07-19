using System.Collections.Concurrent;

namespace CtxApp.Infrastructure.Security;

/// <summary>Stores the ECDSA public key each device signs its requests with.</summary>
public interface IDeviceKeyRegistry
{
    /// <summary>Record (or replace) the uncompressed P-256 public key for a device.</summary>
    void Register(string deviceId, byte[] uncompressedPublicKey);

    /// <summary>The device's uncompressed public key, or null if it is not enrolled.</summary>
    byte[]? PublicKey(string deviceId);
}

/// <summary>
/// In-process device key registry. A generated app swaps this for a persistent
/// implementation by registering its own <see cref="IDeviceKeyRegistry"/>.
/// </summary>
public sealed class InMemoryDeviceKeyRegistry : IDeviceKeyRegistry
{
    private readonly ConcurrentDictionary<string, byte[]> _keys = new();

    public void Register(string deviceId, byte[] uncompressedPublicKey)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("Device id is required.", nameof(deviceId));
        }
        if (uncompressedPublicKey.Length != 65 || uncompressedPublicKey[0] != 0x04)
        {
            throw new ArgumentException("Expected a 65-byte uncompressed P-256 public key.", nameof(uncompressedPublicKey));
        }
        _keys[deviceId] = uncompressedPublicKey;
    }

    public byte[]? PublicKey(string deviceId) => _keys.GetValueOrDefault(deviceId);
}
