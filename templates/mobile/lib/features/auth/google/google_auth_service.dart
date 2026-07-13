import 'package:google_sign_in/google_sign_in.dart';

/// Thin wrapper over the google_sign_in plugin so the auth Bloc stays
/// testable. Returns the Google ID token, which the API exchanges for a
/// session (POST /v1/users/google-sign-in).
class GoogleAuthService {
  bool _initialized = false;

  /// Returns the ID token, or null if the user cancelled the flow.
  Future<String?> signIn() async {
    final signIn = GoogleSignIn.instance;
    if (!_initialized) {
      await signIn.initialize();
      _initialized = true;
    }
    try {
      final account = await signIn.authenticate();
      return account.authentication.idToken;
    } on GoogleSignInException catch (e) {
      if (e.code == GoogleSignInExceptionCode.canceled) return null;
      rethrow;
    }
  }
}
