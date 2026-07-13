import 'package:http/http.dart' as http;

/// An interceptor that injects the X-Client-Version header and
/// checks for 426 Upgrade Required responses (docs/HTTP_HANDLING.md).
class VersionCheckClient extends http.BaseClient {
  VersionCheckClient({
    required http.Client inner,
    required this.clientVersion,
    required this.onUpgradeRequired,
  }) : _inner = inner;

  final http.Client _inner;
  final String clientVersion;
  final void Function() onUpgradeRequired;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    request.headers['X-Client-Version'] = clientVersion;

    final response = await _inner.send(request);

    if (response.statusCode == 426) {
      onUpgradeRequired();
    }

    return response;
  }
}
