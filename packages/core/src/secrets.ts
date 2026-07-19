import crypto from 'node:crypto';

/**
 * Generation of the server-side secrets a generated workspace's API reads from
 * its environment.
 *
 * This lives in the engine rather than in a frontend because the encodings are
 * part of the wire protocol (see `protocol/wire-protocol.md`): the ALE key pair
 * must be produced exactly as the .NET API and the Flutter client expect, and
 * every frontend — the `ctx0` CLI, the MCP engine server, the portal — must emit
 * identical material. A frontend's job is to present the result, not to derive it.
 */

/**
 * The server secrets, keyed by the environment variable name the API reads.
 * Ordered as emitted: ALE key pair, JWT signing key, then envelope encryption.
 */
export interface ServerSecrets {
  /** P-256 private scalar, 32 raw bytes, base64. */
  Ctx__Ale__PrivateKey: string;
  /** P-256 public point, uncompressed (0x04 || X || Y), 65 raw bytes, base64. */
  Ctx__Ale__PublicKey: string;
  /** 48 random bytes, base64. */
  Ctx__Jwt__SigningKey: string;
  /** Key-encryption key version 1: 32 random bytes, base64. */
  Ctx__Envelope__Keks__1: string;
  /** The KEK version envelope encryption writes with. */
  Ctx__Envelope__ActiveKekVersion: string;
  /** Blind-index HMAC key: 32 random bytes, base64. */
  Ctx__Envelope__BlindIndexKey: string;
}

/**
 * Generate a fresh set of server secrets: a NIST P-256 (prime256v1) key pair for
 * the ALE server key, plus random JWT, KEK and blind-index keys.
 *
 * The private key is the raw 32-byte scalar and the public key the uncompressed
 * point — the same encodings the API and the Flutter client use — both base64.
 */
export function generateServerSecrets(): ServerSecrets {
  const { publicKey, privateKey } = crypto.generateKeyPairSync('ec', { namedCurve: 'prime256v1' });

  const pubJwk = publicKey.export({ format: 'jwk' }) as { x: string; y: string };
  const privJwk = privateKey.export({ format: 'jwk' }) as { d: string };

  const x = Buffer.from(pubJwk.x, 'base64url');
  const y = Buffer.from(pubJwk.y, 'base64url');
  const d = Buffer.from(privJwk.d, 'base64url');

  const uncompressed = Buffer.concat([Buffer.from([0x04]), leftPad(x, 32), leftPad(y, 32)]);

  return {
    Ctx__Ale__PrivateKey: leftPad(d, 32).toString('base64'),
    Ctx__Ale__PublicKey: uncompressed.toString('base64'),
    Ctx__Jwt__SigningKey: crypto.randomBytes(48).toString('base64'),
    Ctx__Envelope__Keks__1: crypto.randomBytes(32).toString('base64'),
    Ctx__Envelope__ActiveKekVersion: '1',
    Ctx__Envelope__BlindIndexKey: crypto.randomBytes(32).toString('base64'),
  };
}

/** Left-pad (or left-truncate) a big-endian integer to a fixed byte length. */
function leftPad(buf: Buffer, length: number): Buffer {
  if (buf.length === length) return buf;
  if (buf.length > length) return buf.subarray(buf.length - length);
  return Buffer.concat([Buffer.alloc(length - buf.length), buf]);
}
