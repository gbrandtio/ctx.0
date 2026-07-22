using System.Security.Cryptography;
using CtxApp.Infrastructure.Security.Crypto;
using Microsoft.Extensions.Configuration;

namespace CtxApp.Infrastructure.Security;

/// <summary>Supplies the server's static ALE (ECDH) key pair.</summary>
public interface IAleKeyProvider
{
    /// <summary>The server's private parameters, used to open request envelopes.</summary>
    ECParameters PrivateParameters { get; }

    /// <summary>The server's uncompressed public key, published to clients.</summary>
    byte[] PublicKey { get; }
}

/// <summary>
/// Loads the server ALE key pair from the environment:
/// <c>CTX_ALE_PRIVATE_KEY</c> (base64 raw 32-byte scalar) and
/// <c>CTX_ALE_PUBLIC_KEY</c> (base64 uncompressed point). Generate a pair with
/// <c>ctx0 keygen</c>. Construction fails fast when the keys are absent.
/// </summary>
public sealed class ConfigAleKeyProvider : IAleKeyProvider
{
    public ConfigAleKeyProvider(IConfiguration configuration)
    {
        var privateB64 = configuration["CTX_ALE_PRIVATE_KEY"]
            ?? throw new InvalidOperationException("CTX_ALE_PRIVATE_KEY is not configured. Run `ctx0 keygen`.");
        var publicB64 = configuration["CTX_ALE_PUBLIC_KEY"]
            ?? throw new InvalidOperationException("CTX_ALE_PUBLIC_KEY is not configured. Run `ctx0 keygen`.");

        PublicKey = Convert.FromBase64String(publicB64);
        PrivateParameters = P256.PrivateParams(Convert.FromBase64String(privateB64), PublicKey);
    }

    public ECParameters PrivateParameters { get; }

    public byte[] PublicKey { get; }
}
