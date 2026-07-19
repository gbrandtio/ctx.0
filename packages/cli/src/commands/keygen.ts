import pc from 'picocolors';
import { withEngine } from '../engine.js';

/**
 * `ctx0 keygen` — print the server secrets as the environment variables the API
 * reads. The engine generates them (the encodings are part of the wire
 * protocol); this command only presents them.
 */
export async function runKeygen(): Promise<void> {
  const { secrets } = await withEngine((engine) => engine.call('secrets.generate', {}));

  console.log(pc.bold('\nServer secrets (set these as environment variables):\n'));
  for (const [name, value] of Object.entries(secrets)) {
    console.log(`${name}=${value}`);
  }
  console.log(
    pc.dim('\nKeep every value secret; provide them to the API via the environment only.\n'),
  );
}
