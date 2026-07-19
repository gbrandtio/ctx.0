/// Constants that define the ctx.0 wire protocol on the client. These mirror the
/// API's `CtxProtocol` exactly; both test suites assert against the shared golden
/// vectors in the workspace's `.ctx/vectors.json`.
class CtxProtocol {
  const CtxProtocol._();

  /// Protocol version advertised in the `X-Ctx-Protocol` header.
  static const String version = '1.0';

  static const String protocolHeader = 'X-Ctx-Protocol';
  static const String deviceIdHeader = 'X-Ctx-Device-Id';
  static const String timestampHeader = 'X-Ctx-Timestamp';
  static const String signatureHeader = 'X-Ctx-Signature';

  /// HKDF `info` string binding derived keys to this scheme/version.
  static const String aleHkdfInfo = 'ctx-ale-v1';
}
