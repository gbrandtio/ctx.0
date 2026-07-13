import '../models/problem_details.dart';

/// The single error type propagated Repository → Bloc
/// (docs/ERROR_HANDLING.md §2).
class AppException implements Exception {
  const AppException(this.problem);

  /// Wraps any error into an [AppException]; unexpected errors become a
  /// generic 500 so the UI never sees raw exception text.
  factory AppException.from(Object error) {
    if (error is AppException) return error;
    return const AppException(ProblemDetails(status: 500));
  }

  final ProblemDetails problem;

  int? get status => problem.status;

  /// 4xx errors (except 401) carry API-authored messages that are safe to
  /// surface to the user verbatim.
  bool get isClientSafe {
    final s = status;
    return s != null && s >= 400 && s < 500 && s != 401;
  }

  /// The only message the UI is allowed to display (docs/ERROR_HANDLING.md
  /// §3 Rule 1). Server 5xx details are never shown.
  String get userFriendlyMessage {
    if (isClientSafe) {
      final detail = problem.detail;
      if (detail != null && detail.isNotEmpty) return detail;
      final title = problem.title;
      if (title != null && title.isNotEmpty) return title;
    }
    if (status == 401) return 'Your session has expired. Please log in again.';
    return 'Something went wrong. Please try again later.';
  }

  String? get traceId => problem.traceId;

  @override
  String toString() =>
      'AppException(status: $status, title: ${problem.title}, '
      'traceId: ${problem.traceId})';
}
