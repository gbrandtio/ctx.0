namespace Acme.Api.Security;

/// <summary>
/// The ALE context for one request: the AES key derived from the client's
/// ephemeral key (reused to seal the response) and the decrypted request body.
/// Stashed in <c>HttpContext.Items</c> by the secure endpoint filter.
/// </summary>
public sealed class AleSession(byte[] key, byte[] requestPlaintext)
{
    public const string ItemKey = "ctx.ale.session";

    public byte[] Key { get; } = key;

    public byte[] RequestPlaintext { get; } = requestPlaintext;
}
