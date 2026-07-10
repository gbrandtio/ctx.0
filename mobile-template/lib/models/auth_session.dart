import 'package:equatable/equatable.dart';

import 'user.dart';

/// Login/signup/refresh response: rotating token pair + the user.
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
      user: User.fromJson(json['user'] as Map<String, dynamic>),
    );
  }

  final String accessToken;
  final String refreshToken;
  final User user;

  @override
  List<Object?> get props => [accessToken, refreshToken, user];
}
