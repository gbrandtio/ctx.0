import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../../core/constants/api_constants.dart';
import '../../../models/auth_session.dart';
import '../../../models/user.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';

import 'mixins/api_base_mixin.dart';

/// User & auth endpoints, matching the shipped API (docs/API/swagger.json).
/// Auth/session calls always bypass the cache
/// (docs/CACHING_IMPLEMENTATION.md "Mandatory Bypass").
class UserApiService with ApiBaseMixin {
  UserApiService(this._client);

  final http.Client _client;

  static const _jsonHeaders = {'Content-Type': 'application/json'};
  static const _noCacheHeaders = {CachingClient.bypassHeader: 'true'};

  /// Step 1 of registration: emails a verification code
  /// (AUTHENTICATION.md — /users/register/send-code).
  Future<void> sendSignupCode(String email) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.sendSignupCode),
      headers: _jsonHeaders,
      body: jsonEncode({'email': email}),
    );
    decodeResponse(response);
  }

  /// Step 2: creates the account with the verification code, returning a
  /// session (POST /users).
  Future<AuthSession> register({
    required String username,
    required String email,
    required String password,
    required String verificationCode,
    String? name,
    required Map<String, bool> consents,
  }) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.users),
      headers: _jsonHeaders,
      body: jsonEncode({
        'username': username,
        'email': email,
        'password': password,
        'verificationCode': verificationCode,
        'name': name,
        'consents': consents,
      }),
    );
    return AuthSession.fromJson(
      decodeResponse(response) as Map<String, dynamic>,
    );
  }

  Future<AuthSession> login(String usernameOrEmail, String password) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.login),
      headers: _jsonHeaders,
      body: jsonEncode({
        'usernameOrEmail': usernameOrEmail,
        'password': password,
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

  Future<void> logout(String refreshToken) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.logout),
      headers: _jsonHeaders,
      body: jsonEncode({'refreshToken': refreshToken}),
    );
    decodeResponse(response);
  }

  Future<User> getUser(String userId, {bool forceRefresh = false}) async {
    final response = await _client.get(
      ApiConstants.uri(ApiConstants.user(userId)),
      headers: forceRefresh ? _noCacheHeaders : null,
    );
    return User.fromJson(decodeResponse(response) as Map<String, dynamic>);
  }

  Future<User> updateUser(
    String userId, {
    String? displayName,
    bool? hasTrackingConsent,
  }) async {
    final response = await _client.patch(
      ApiConstants.uri(ApiConstants.user(userId)),
      headers: _jsonHeaders,
      body: jsonEncode({
        'name': ?displayName,
        'hasTrackingConsent': ?hasTrackingConsent,
      }),
    );
    return User.fromJson(decodeResponse(response) as Map<String, dynamic>);
  }

  /// GDPR anonymizing delete (docs/APP_SHELL.md §4).
  Future<void> deleteUser(String userId) async {
    final response = await _client.delete(
      ApiConstants.uri(ApiConstants.user(userId)),
    );
    decodeResponse(response);
  }

  /// GDPR data export request; completion is delivered via push.
  Future<void> requestDataExport(String userId) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.userExports(userId)),
      headers: _jsonHeaders,
    );
    decodeResponse(response);
  }
}
