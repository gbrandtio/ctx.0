import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../../core/constants/api_constants.dart';
import '../../../models/auth_session.dart';
import '../../../models/user.dart';
import 'interceptors/caching_client.dart';
import 'mixins/api_base_mixin.dart';

/// User & auth endpoints. Auth/session calls always bypass the cache
/// (docs/CACHING_IMPLEMENTATION.md "Mandatory Bypass").
class UserApiService with ApiBaseMixin {
  UserApiService(this._client);

  final http.Client _client;

  static const _jsonHeaders = {'Content-Type': 'application/json'};
  static const _noCacheHeaders = {CachingClient.bypassHeader: 'true'};

  Future<AuthSession> login(String email, String password) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.login),
      headers: _jsonHeaders,
      body: jsonEncode({'email': email, 'password': password}),
    );
    return AuthSession.fromJson(
      decodeResponse(response) as Map<String, dynamic>,
    );
  }

  Future<AuthSession> signup({
    required String email,
    required String password,
    String? displayName,
    required Map<String, bool> consents,
  }) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.users),
      headers: _jsonHeaders,
      body: jsonEncode({
        'email': email,
        'password': password,
        'displayName': displayName,
        'consents': consents,
      }),
    );
    return AuthSession.fromJson(
      decodeResponse(response) as Map<String, dynamic>,
    );
  }

  Future<AuthSession> googleSignIn(String idToken) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.googleSignIn),
      headers: _jsonHeaders,
      body: jsonEncode({'idToken': idToken}),
    );
    return AuthSession.fromJson(
      decodeResponse(response) as Map<String, dynamic>,
    );
  }

  Future<void> verifyEmail(String code) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.verifyEmail),
      headers: _jsonHeaders,
      body: jsonEncode({'code': code}),
    );
    decodeResponse(response);
  }

  Future<void> resendVerification() async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.resendVerification),
      headers: _jsonHeaders,
    );
    decodeResponse(response);
  }

  Future<void> logout(String refreshToken) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.logout),
      headers: _jsonHeaders,
      body: jsonEncode({'refreshToken': refreshToken}),
    );
    decodeResponse(response);
  }

  Future<User> getMe({bool forceRefresh = false}) async {
    final response = await _client.get(
      ApiConstants.uri(ApiConstants.me),
      headers: forceRefresh ? _noCacheHeaders : null,
    );
    return User.fromJson(decodeResponse(response) as Map<String, dynamic>);
  }

  Future<User> updateMe({String? displayName}) async {
    final response = await _client.patch(
      ApiConstants.uri(ApiConstants.me),
      headers: _jsonHeaders,
      body: jsonEncode({'displayName': displayName}),
    );
    return User.fromJson(decodeResponse(response) as Map<String, dynamic>);
  }

  /// GDPR anonymizing delete (docs/APP_SHELL.md §4).
  Future<void> deleteMe() async {
    final response = await _client.delete(ApiConstants.uri(ApiConstants.me));
    decodeResponse(response);
  }

  /// GDPR data export request; completion is delivered via push.
  Future<void> requestDataExport() async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.myExports),
      headers: _jsonHeaders,
    );
    decodeResponse(response);
  }
}
