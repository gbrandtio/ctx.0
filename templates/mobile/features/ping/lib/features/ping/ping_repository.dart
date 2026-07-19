import '../../security/ctx_security.dart';

/// Talks to the secure `/v1/ping` endpoint through the ctx.0 wire protocol.
class PingRepository {
  const PingRepository(this._client);

  final SecureHttpClient _client;

  /// Send [message] and return the server's echo.
  Future<String> ping(String message) async {
    final reply = await _client.secureSend('POST', '/v1/ping', {'message': message});
    return reply['echo'] as String;
  }
}
