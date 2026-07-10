import 'package:http/http.dart' as http;

/// Shared helpers for the interceptor chain (docs/HTTP_HANDLING.md
/// "Interceptor Orchestration").
abstract final class HttpInterceptorUtils {
  /// Rebuilds an equivalent un-finalized request so it can be re-sent
  /// (interceptors may only retry [http.Request]s — streamed/multipart
  /// bodies cannot be replayed safely).
  static http.Request copyRequest(http.Request original) {
    return http.Request(original.method, original.url)
      ..headers.addAll(original.headers)
      ..bodyBytes = original.bodyBytes
      ..followRedirects = original.followRedirects
      ..maxRedirects = original.maxRedirects
      ..persistentConnection = original.persistentConnection;
  }

  /// Buffers a streamed response so the body can be inspected, returning
  /// a replayable equivalent.
  static Future<http.Response> buffer(http.StreamedResponse response) =>
      http.Response.fromStream(response);

  /// Converts a buffered [http.Response] back to the [http.StreamedResponse]
  /// shape the `send` contract requires.
  static http.StreamedResponse toStreamed(http.Response response) {
    return http.StreamedResponse(
      Stream.value(response.bodyBytes),
      response.statusCode,
      contentLength: response.bodyBytes.length,
      request: response.request,
      headers: response.headers,
      isRedirect: response.isRedirect,
      persistentConnection: response.persistentConnection,
      reasonPhrase: response.reasonPhrase,
    );
  }
}
