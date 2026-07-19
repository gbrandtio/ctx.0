import 'dart:convert';
import 'dart:typed_data';

import 'package:http/http.dart' as http;
import 'package:http_parser/http_parser.dart';

import '../../auth/data/token_store.dart';

/// One stored file as returned by the API. Bytes are fetched separately from
/// [MediaRepository.downloadUri] (an authenticated request), not embedded here.
class MediaItem {
  const MediaItem({
    required this.id,
    required this.fileName,
    required this.contentType,
    required this.sizeBytes,
    required this.createdAt,
  });

  final String id;
  final String fileName;
  final String contentType;
  final int sizeBytes;
  final DateTime createdAt;

  bool get isImage => contentType.startsWith('image/');

  factory MediaItem.fromJson(Map<String, dynamic> json) => MediaItem(
        id: json['id'] as String,
        fileName: json['fileName'] as String,
        contentType: json['contentType'] as String,
        sizeBytes: (json['sizeBytes'] as num).toInt(),
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

/// Raised when a media request fails.
class MediaException implements Exception {
  const MediaException(this.message);
  final String message;
  @override
  String toString() => message;
}

/// Reads and mutates the signed-in user's stored files.
abstract class MediaRepository {
  Future<List<MediaItem>> list();
  Future<MediaItem> upload({required String fileName, required String contentType, required Uint8List bytes});
  Future<void> delete(String id);

  /// URL for the raw bytes of [id]; the request must still carry the bearer token.
  Uri downloadUri(String id);
}

/// [MediaRepository] backed by the JWT-protected `/v1/media` endpoints. Files are
/// per-user and RLS-isolated on the server, so every request carries the access
/// token minted by the auth feature (plain authenticated HTTP, not the ALE
/// `secureSend` client, which carries no user identity).
class HttpMediaRepository implements MediaRepository {
  HttpMediaRepository(this._tokens, {String? baseUrl, http.Client? client})
      : _baseUrl = baseUrl ?? const String.fromEnvironment('CTX_API_BASE_URL', defaultValue: 'http://localhost:5080'),
        _http = client ?? http.Client();

  final TokenStore _tokens;
  final String _baseUrl;
  final http.Client _http;

  @override
  Future<List<MediaItem>> list() async {
    final response = await _http.get(Uri.parse('$_baseUrl/v1/media/'), headers: await _headers());
    final json = _decode(response);
    final items = (json['items'] as List<dynamic>).cast<Map<String, dynamic>>();
    return items.map(MediaItem.fromJson).toList();
  }

  @override
  Future<MediaItem> upload({required String fileName, required String contentType, required Uint8List bytes}) async {
    final request = http.MultipartRequest('POST', Uri.parse('$_baseUrl/v1/media/'))
      ..headers.addAll(await _headers())
      ..files.add(http.MultipartFile.fromBytes(
        'file',
        bytes,
        filename: fileName,
        contentType: MediaType.parse(contentType),
      ));
    final response = await http.Response.fromStream(await _http.send(request));
    return MediaItem.fromJson(_decode(response));
  }

  @override
  Future<void> delete(String id) async {
    final response = await _http.delete(Uri.parse('$_baseUrl/v1/media/$id'), headers: await _headers());
    if (response.statusCode >= 400) {
      throw MediaException('Delete failed (${response.statusCode})');
    }
  }

  @override
  Uri downloadUri(String id) => Uri.parse('$_baseUrl/v1/media/$id');

  Future<Map<String, String>> _headers() async {
    final token = await _tokens.readAccessToken();
    if (token == null) throw const MediaException('Not signed in');
    return {'Authorization': 'Bearer $token'};
  }

  Map<String, dynamic> _decode(http.Response response) {
    if (response.statusCode >= 400) {
      throw MediaException('Request failed (${response.statusCode})');
    }
    if (response.body.isEmpty) return const {};
    return jsonDecode(response.body) as Map<String, dynamic>;
  }
}
