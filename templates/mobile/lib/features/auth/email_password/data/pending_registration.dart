/// Registration data collected on the signup screen, carried to the
/// verify-email screen (as a GoRouter `extra`) where it is submitted with
/// the emailed code. The API creates the account only at that second step
/// (AUTHENTICATION.md — send-code → register).
class PendingRegistration {
  const PendingRegistration({
    required this.username,
    required this.email,
    required this.password,
    required this.consents,
    this.displayName,
  });

  final String username;
  final String email;
  final String password;
  final String? displayName;
  final Map<String, bool> consents;

  /// Derives a username from the email local-part plus a short suffix when
  /// the signup form does not collect one (the API requires 3–40 chars).
  factory PendingRegistration.fromForm({
    required String email,
    required String password,
    String? displayName,
    required Map<String, bool> consents,
  }) {
    final localPart = email.split('@').first.replaceAll(
          RegExp(r'[^a-zA-Z0-9_]'),
          '',
        );
    final base = localPart.isEmpty ? 'user' : localPart;
    final suffix = (DateTime.now().microsecondsSinceEpoch % 10000)
        .toString()
        .padLeft(4, '0');
    final username =
        '${base.substring(0, base.length.clamp(0, 35))}$suffix';
    return PendingRegistration(
      username: username,
      email: email,
      password: password,
      displayName: displayName,
      consents: consents,
    );
  }
}
