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
  static const bool useMockData = bool.fromEnvironment(
    'USE_MOCK_DATA',
    defaultValue: false,
  );

  // ---- Endpoints (paths match the shipped API's swagger.json;
  // see docs/API/swagger.json and the API's AUTHENTICATION.md) ----
  static const String users = '/users';
  static const String sendSignupCode = '/users/register/send-code';
  static const String login = '/users/authenticate';
  static const String refreshToken = '/users/refresh';
  static const String logout = '/users/logout';
  static const String googleSignIn = '/users/google/authenticate';
  static const String notifications = '/users/notifications';
  static const String firebaseToken = '/users/firebase/token';
  static const String paymentIntents = '/payments/intents';
  static const String itemsNearby = '/items/nearby';
  static const String securityMetadata = '/security/metadata';
  static const String appInstances = '/security/app-instances';

  /// Per-user resources are addressed by id ({userId} + UserSelf policy),
  /// not a `/me` alias.
  static String user(String userId) => '/users/$userId';
  static String userExports(String userId) => '/users/$userId/exports';

  static Uri uri(String path, [Map<String, dynamic>? query]) => Uri.parse(
    '$baseUrl/$apiVersion$path',
  ).replace(queryParameters: query?.map((k, v) => MapEntry(k, v.toString())));
}
