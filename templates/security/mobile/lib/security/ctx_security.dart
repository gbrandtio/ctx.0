import 'secure_http_client.dart';

export 'rasp_gate.dart';
export 'secure_http_client.dart';

/// The app-wide secure API client. Its base URL comes from the environment
/// (`--dart-define=CTX_API_BASE_URL=...`). Every repository sends through this.
final SecureHttpClient ctxSecureClient = SecureHttpClient(
  baseUrl: const String.fromEnvironment('CTX_API_BASE_URL', defaultValue: 'http://localhost:5080'),
);
