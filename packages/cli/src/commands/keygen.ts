import crypto from 'node:crypto';
import pc from 'picocolors';

/**
 * Generate a NIST P-256 key pair for the server ALE key, printed as the two
 * environment variables the API reads. The private key is a raw 32-byte scalar;
 * the public key is an uncompressed point — the same encodings the API and the
 * Flutter client use.
 */
export function runKeygen(): void {
  const { publicKey, privateKey } = crypto.generateKeyPairSync('ec', { namedCurve: 'prime256v1' });

  const pubJwk = publicKey.export({ format: 'jwk' }) as { x: string; y: string };
  const privJwk = privateKey.export({ format: 'jwk' }) as { d: string };

  const x = Buffer.from(pubJwk.x, 'base64url');
  const y = Buffer.from(pubJwk.y, 'base64url');
  const d = Buffer.from(privJwk.d, 'base64url');

  const uncompressed = Buffer.concat([Buffer.from([0x04]), leftPad(x, 32), leftPad(y, 32)]);
  const privateB64 = leftPad(d, 32).toString('base64');
  const publicB64 = uncompressed.toString('base64');

  const jwtSigningKey = crypto.randomBytes(48).toString('base64');
  const kek = crypto.randomBytes(32).toString('base64');
  const blindIndexKey = crypto.randomBytes(32).toString('base64');

  console.log(pc.bold('\nServer secrets (set these as environment variables):\n'));
  console.log(`Ctx__Ale__PrivateKey=${privateB64}`);
  console.log(`Ctx__Ale__PublicKey=${publicB64}`);
  console.log(`Ctx__Jwt__SigningKey=${jwtSigningKey}`);
  console.log(`Ctx__Envelope__Keks__1=${kek}`);
  console.log(`Ctx__Envelope__ActiveKekVersion=1`);
  console.log(`Ctx__Envelope__BlindIndexKey=${blindIndexKey}`);
  console.log(pc.dim('\nKeep every value secret; provide them to the API via the environment only.\n'));
}

function leftPad(buf: Buffer, length: number): Buffer {
  if (buf.length === length) return buf;
  if (buf.length > length) return buf.subarray(buf.length - length);
  return Buffer.concat([Buffer.alloc(length - buf.length), buf]);
}
