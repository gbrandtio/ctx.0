import 'package:equatable/equatable.dart';

import 'user.dart';

/// Login/signup/refresh response. The API returns a FLAT shape
/// (docs/API/swagger.json — AuthResponse: accessToken, refreshToken,
/// expiresAtUtc, userId, username, email); the user is reconstructed from
/// those fields.
class AuthSession extends Equatable {
  const AuthSession({
    required this.accessToken,
    required this.refreshToken,
    required this.user,
  });

  factory AuthSession.fromJson(Map<String, dynamic> json) {
    return AuthSession(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
      user: User(
        id: json['userId'].toString(),
        email: json['email'] as String,
        username: json['username'] as String?,
      ),
    );
  }

  final String accessToken;
  final String refreshToken;
  final User user;

  @override
  List<Object?> get props => [accessToken, refreshToken, user];
}
