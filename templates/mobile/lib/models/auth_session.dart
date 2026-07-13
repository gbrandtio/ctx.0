import 'package:equatable/equatable.dart';

import 'user.dart';

/// Login/signup/refresh response. The API returns a FLAT shape
/// (docs/API/swagger.json — AuthResponse: accessToken, refreshToken,
/// expiresAtUtc, userId, username, email); the user is reconstructed from
/// those fields.
class AuthSession extends Equatable {
  const AuthSession({
    this.accessToken,
    this.refreshToken,
    required this.user,
    this.requiresTwoFactor = false,
  });

  factory AuthSession.fromJson(Map<String, dynamic> json) {
    return AuthSession(
      accessToken: json['accessToken'] as String?,
      refreshToken: json['refreshToken'] as String?,
      requiresTwoFactor: json['requiresTwoFactor'] as bool? ?? false,
      user: User(
        id: json['userId'].toString(),
        email: json['email'] as String,
        username: json['username'] as String?,
      ),
    );
  }

  final String? accessToken;
  final String? refreshToken;
  final User user;
  final bool requiresTwoFactor;

  @override
  List<Object?> get props => [accessToken, refreshToken, user, requiresTwoFactor];
}
