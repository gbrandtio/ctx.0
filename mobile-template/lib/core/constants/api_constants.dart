/// API endpoint constants (docs/HTTP_HANDLING.md). Values that vary per
/// environment come from --dart-define (docs/ENVIRONMENT_VARIABLES.md).
abstract final class ApiConstants {
  /// Base URL of the backend API, e.g. https://api.example.com.
  static const String baseUrl = String.fromEnvironment('API_BASE_URL');

  /// Google Maps API key (consumed by the maps feature module).
  static const String mapsApiKey = String.fromEnvironment('MAPS_API_KEY');

  static const String apiVersion = 'v1';

  /// When true, ApiServices return simulated data instead of hitting the
  /// network — lets feature work proceed before the API is reachable.
  static const bool useMockData =
      bool.fromEnvironment('USE_MOCK_DATA', defaultValue: false);

  // ---- Endpoints (paths are relative to `$baseUrl/$apiVersion`) ----
  static const String users = '/users';
  static const String login = '/users/login';
  static const String refreshToken = '/users/refresh-token';
  static const String logout = '/users/logout';
  static const String verifyEmail = '/users/verify-email';
  static const String resendVerification = '/users/resend-verification';
  static const String googleSignIn = '/users/google-sign-in';
  static const String me = '/users/me';
  static const String myExports = '/users/me/exports';
  static const String notifications = '/users/notifications';
  static const String securityMetadata = '/security/metadata';
  static const String appInstances = '/security/app-instances';

  static Uri uri(String path, [Map<String, dynamic>? query]) =>
      Uri.parse('$baseUrl/$apiVersion$path').replace(
        queryParameters:
            query?.map((k, v) => MapEntry(k, v.toString())),
      );
}
