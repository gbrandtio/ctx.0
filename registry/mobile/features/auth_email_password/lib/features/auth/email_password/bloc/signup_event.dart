part of 'signup_bloc.dart';

sealed class SignupEvent {
  const SignupEvent();
}

final class SignupSubmitted extends SignupEvent {
  const SignupSubmitted({
    required this.email,
    required this.password,
    this.displayName,
    required this.consents,
  });

  final String email;
  final String password;
  final String? displayName;

  /// Consent id → granted (docs/APP_SHELL.md §4).
  final Map<String, bool> consents;
}
