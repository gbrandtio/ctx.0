import 'package:equatable/equatable.dart';

/// The authenticated user as returned by the API (docs/HTTP_HANDLING.md —
/// response models live in lib/models/).
class User extends Equatable {
  const User({
    required this.id,
    required this.email,
    this.displayName,
    this.emailVerified = false,
  });

  factory User.fromJson(Map<String, dynamic> json) {
    return User(
      id: json['id'] as String,
      email: json['email'] as String,
      displayName: json['displayName'] as String?,
      emailVerified: json['emailVerified'] as bool? ?? false,
    );
  }

  final String id;
  final String email;
  final String? displayName;
  final bool emailVerified;

  Map<String, dynamic> toJson() => {
        'id': id,
        'email': email,
        'displayName': displayName,
        'emailVerified': emailVerified,
      };

  /// Background refreshes can return bare models; merge with copyWith and
  /// never overwrite richer local data (docs/FLUTTER_ARCHITECTURE.md §6D).
  User copyWith({String? displayName, bool? emailVerified}) {
    return User(
      id: id,
      email: email,
      displayName: displayName ?? this.displayName,
      emailVerified: emailVerified ?? this.emailVerified,
    );
  }

  @override
  List<Object?> get props => [id, email, displayName, emailVerified];
}
