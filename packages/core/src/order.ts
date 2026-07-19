/**
 * Deterministic ordering primitives.
 *
 * Every list the engine derives from the filesystem — directory entries, the
 * file list recorded in the workspace manifest, the walk order feeding an
 * overlay hash — is sorted with the *same* comparator, so a given template tree
 * always composes to the same result on any host, and an overlay hash computed
 * by one implementation matches one computed by another.
 *
 * The comparator is a byte-wise comparison of the UTF-8 encoding of each string.
 * It is chosen because it is the ordering every language reaches for by default
 * (Go and Rust compare strings this way natively, as does `LC_ALL=C sort`), and
 * because it is locale-independent. Note that it is *not* JavaScript's default
 * `Array.prototype.sort`, which compares UTF-16 code units and therefore orders
 * some non-BMP characters differently.
 */

/** Compare two strings by the bytes of their UTF-8 encoding. */
export function compareUtf8(a: string, b: string): number {
  return Buffer.compare(Buffer.from(a, 'utf8'), Buffer.from(b, 'utf8'));
}

/** A new array holding `values` in UTF-8 byte order. */
export function sortUtf8(values: readonly string[]): string[] {
  return [...values].sort(compareUtf8);
}
