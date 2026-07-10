/// RFC 9457 Problem Details — the unified error body every API error
/// response (4xx/5xx) follows. See docs/ERROR_HANDLING.md §1.
class ProblemDetails {
  const ProblemDetails({
    this.status,
    this.title,
    this.detail,
    this.instance,
    this.traceId,
  });

  factory ProblemDetails.fromJson(Map<String, dynamic> json) {
    return ProblemDetails(
      status: json['status'] as int?,
      title: json['title'] as String?,
      detail: json['detail'] as String?,
      instance: json['instance'] as String?,
      traceId: json['traceId'] as String?,
    );
  }

  /// Fallback for non-JSON error bodies (gateway timeouts, security layer
  /// failures) so the rest of the app always deals with one shape.
  factory ProblemDetails.fallback(int statusCode) {
    return ProblemDetails(
      status: statusCode,
      title: 'Internal Server Error',
      detail: null,
    );
  }

  final int? status;
  final String? title;
  final String? detail;
  final String? instance;
  final String? traceId;
}
