import 'package:equatable/equatable.dart';

/// The authenticated user (docs/API/swagger.json — UserResponse:
/// {id, username, email, name, createdAt}). The API verifies the email at
/// registration, so an existing account is always verified.
class User extends Equatable {
  const User({
    required this.id,
    required this.email,
    this.username,
    this.displayName,
  });

  factory User.fromJson(Map<String, dynamic> json) {
    return User(
      id: json['id'].toString(),
      email: json['email'] as String,
      username: json['username'] as String?,
      displayName: json['name'] as String?,
    );
  }

  final String id;
  final String email;
  final String? username;

  /// Maps to the API's `name` field.
  final String? displayName;

  /// Background refreshes can return bare models; merge with copyWith and
  /// never overwrite richer local data (docs/FLUTTER_ARCHITECTURE.md §6D).
  User copyWith({String? displayName}) {
    return User(
      id: id,
      email: email,
      username: username,
      displayName: displayName ?? this.displayName,
    );
  }

  @override
  List<Object?> get props => [id, email, username, displayName];
}
