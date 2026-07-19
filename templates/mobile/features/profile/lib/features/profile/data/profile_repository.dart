import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../auth/data/token_store.dart';

/// The signed-in user's account profile as returned by the API.
class ProfileData {
  const ProfileData({
    required this.displayName,
    this.bio,
    this.avatarUrl,
    this.avatarMediaId,
    this.updatedAt,
  });

  final String displayName;
  final String? bio;
  final String? avatarUrl;
  final String? avatarMediaId;
  final DateTime? updatedAt;

  factory ProfileData.fromJson(Map<String, dynamic> json) => ProfileData(
        displayName: (json['displayName'] as String?) ?? '',
        bio: json['bio'] as String?,
        avatarUrl: json['avatarUrl'] as String?,
        avatarMediaId: json['avatarMediaId'] as String?,
        updatedAt: json['updatedAt'] == null ? null : DateTime.parse(json['updatedAt'] as String),
      );
}

/// Raised when a profile request fails.
class ProfileException implements Exception {
  const ProfileException(this.message);
  final String message;
  @override
  String toString() => message;
}

/// Reads and updates the signed-in user's profile.
abstract class ProfileRepository {
  Future<ProfileData> get();
  Future<ProfileData> update({required String displayName, String? bio, String? avatarUrl});
}

/// [ProfileRepository] backed by the JWT-protected `/v1/profile` endpoints. The
/// profile is per-user and RLS-isolated on the server, so every request carries
/// the access token minted by the auth feature (plain authenticated JSON, not the
/// ALE `secureSend` client, which carries no user identity).
class HttpProfileRepository implements ProfileRepository {
  HttpProfileRepository(this._tokens, {String? baseUrl, http.Client? client})
      : _baseUrl = baseUrl ?? const String.fromEnvironment('CTX_API_BASE_URL', defaultValue: 'http://localhost:5080'),
        _http = client ?? http.Client();

  final TokenStore _tokens;
  final String _baseUrl;
  final http.Client _http;

  @override
  Future<ProfileData> get() async {
    final response = await _http.get(Uri.parse('$_baseUrl/v1/profile/'), headers: await _headers());
    return ProfileData.fromJson(_decode(response));
  }

  @override
  Future<ProfileData> update({required String displayName, String? bio, String? avatarUrl}) async {
    final response = await _http.put(
      Uri.parse('$_baseUrl/v1/profile/'),
      headers: await _headers(),
      body: jsonEncode({'displayName': displayName, 'bio': bio, 'avatarUrl': avatarUrl}),
    );
    return ProfileData.fromJson(_decode(response));
  }

  Future<Map<String, String>> _headers() async {
    final token = await _tokens.readAccessToken();
    if (token == null) throw const ProfileException('Not signed in');
    return {'Content-Type': 'application/json', 'Authorization': 'Bearer $token'};
  }

  Map<String, dynamic> _decode(http.Response response) {
    if (response.statusCode >= 400) {
      throw ProfileException('Request failed (${response.statusCode})');
    }
    if (response.body.isEmpty) return const {};
    return jsonDecode(response.body) as Map<String, dynamic>;
  }
}
