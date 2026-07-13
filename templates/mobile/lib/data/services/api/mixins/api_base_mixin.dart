import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../../../core/models/problem_details.dart';
import '../../../../core/utils/app_exception.dart';

/// Foundation for all API services (docs/ERROR_HANDLING.md §2): maps
/// non-2xx responses to [AppException] with RFC 9457 ProblemDetails, and
/// decodes 2xx bodies with the Safe Double-Decoding pattern
/// (docs/SECURITY.md §4.4).
mixin ApiBaseMixin {
  /// Returns the decoded JSON of a 2xx response, or throws [AppException].
  dynamic decodeResponse(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return null;
      return _safeDoubleDecode(response.body);
    }
    throw AppException(_parseProblem(response));
  }

  ProblemDetails _parseProblem(http.Response response) {
    try {
      final json = jsonDecode(response.body);
      if (json is Map<String, dynamic>) {
        final problem = ProblemDetails.fromJson(json);
        return problem.status == null
            ? ProblemDetails(
                status: response.statusCode,
                title: problem.title,
                detail: problem.detail,
                instance: problem.instance,
                traceId: problem.traceId,
              )
            : problem;
      }
    } on FormatException {
      // Not JSON (gateway timeout, security-layer failure) — fall through.
    }
    return ProblemDetails.fallback(response.statusCode);
  }

  /// Decodes once, then again only if the result is a string that looks
  /// like a JSON object/array — never on plain text, which would raise
  /// character-position format errors.
  dynamic _safeDoubleDecode(String body) {
    final first = jsonDecode(body);
    if (first is String) {
      final trimmed = first.trimLeft();
      if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
        return jsonDecode(first);
      }
    }
    return first;
  }
}
