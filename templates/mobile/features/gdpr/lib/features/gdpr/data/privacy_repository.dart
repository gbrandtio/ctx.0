import 'dart:convert';
import 'dart:typed_data';

import 'package:http/http.dart' as http;

import '../../auth/data/token_store.dart';

/// The server's view of the user's consent: the notice version in force, and the
/// decision on record (null when the user has never answered on this account).
class ConsentStatus {
  const ConsentStatus({required this.policyVersion, this.purposes, this.decidedAt, this.recordedVersion});

  /// The privacy-notice version the server currently requires.
  final String policyVersion;

  /// Purposes accepted by the recorded decision, if there is one.
  final Set<String>? purposes;

  final DateTime? decidedAt;

  /// The notice version the recorded decision was made against.
  final String? recordedVersion;

  /// Whether the account's recorded consent is missing or predates the current notice.
  bool get needsDecision => recordedVersion == null || recordedVersion != policyVersion;

  factory ConsentStatus.fromJson(Map<String, dynamic> json) {
    final consent = json['consent'] as Map<String, dynamic>?;
    return ConsentStatus(
      policyVersion: json['policyVersion'] as String,
      purposes: consent == null ? null : ((consent['purposes'] as List<dynamic>?) ?? const []).cast<String>().toSet(),
      decidedAt: consent?['decidedAt'] == null ? null : DateTime.parse(consent!['decidedAt'] as String),
      recordedVersion: consent?['policyVersion'] as String?,
    );
  }
}

/// An export request in flight on the server.
class ExportJob {
  const ExportJob({required this.jobId, required this.status, this.sizeBytes, this.error});

  final String jobId;

  /// One of `Pending`, `Ready`, `Failed`, `Expired`.
  final String status;
  final int? sizeBytes;
  final String? error;

  bool get isReady => status == 'Ready';
  bool get isPending => status == 'Pending';

  factory ExportJob.fromJson(Map<String, dynamic> json) => ExportJob(
        jobId: json['jobId'] as String,
        status: json['status'] as String,
        sizeBytes: (json['sizeBytes'] as num?)?.toInt(),
        error: json['error'] as String?,
      );
}

/// Raised when a privacy request fails.
class PrivacyException implements Exception {
  const PrivacyException(this.message);
  final String message;
  @override
  String toString() => message;
}

/// The data-subject rights the API exposes for the signed-in user.
abstract class PrivacyRepository {
  Future<ConsentStatus> consent();
  Future<ConsentStatus> recordConsent({required String policyVersion, required Set<String> purposes});

  /// Ask for an export. Returns the job and the one-time download token, which
  /// the server shows exactly once.
  Future<({ExportJob job, String downloadToken})> requestExport();
  Future<ExportJob> exportStatus(String jobId);
  Future<Uint8List> downloadExport({required String jobId, required String downloadToken});

  Future<void> deleteAccount({required String password});
}

/// [PrivacyRepository] backed by the JWT-protected `/v1/privacy` endpoints. Like
/// the profile and notes clients this uses plain authenticated JSON rather than
/// the ALE `secureSend` client, because every route is RLS-scoped to the caller
/// and so needs the authenticated identity.
class HttpPrivacyRepository implements PrivacyRepository {
  HttpPrivacyRepository(this._tokens, {String? baseUrl, http.Client? client})
      : _baseUrl = baseUrl ?? const String.fromEnvironment('CTX_API_BASE_URL', defaultValue: 'http://localhost:5080'),
        _http = client ?? http.Client();

  final TokenStore _tokens;
  final String _baseUrl;
  final http.Client _http;

  @override
  Future<ConsentStatus> consent() async {
    final response = await _http.get(Uri.parse('$_baseUrl/v1/privacy/consent'), headers: await _headers());
    return ConsentStatus.fromJson(_decode(response));
  }

  @override
  Future<ConsentStatus> recordConsent({required String policyVersion, required Set<String> purposes}) async {
    final response = await _http.put(
      Uri.parse('$_baseUrl/v1/privacy/consent'),
      headers: await _headers(),
      body: jsonEncode({'policyVersion': policyVersion, 'purposes': purposes.toList()..sort(), 'source': 'app'}),
    );
    return ConsentStatus.fromJson(_decode(response));
  }

  @override
  Future<({ExportJob job, String downloadToken})> requestExport() async {
    final response = await _http.post(Uri.parse('$_baseUrl/v1/privacy/export'), headers: await _headers());
    final json = _decode(response);
    return (job: ExportJob.fromJson(json), downloadToken: json['downloadToken'] as String);
  }

  @override
  Future<ExportJob> exportStatus(String jobId) async {
    final response = await _http.get(Uri.parse('$_baseUrl/v1/privacy/export/$jobId'), headers: await _headers());
    return ExportJob.fromJson(_decode(response));
  }

  @override
  Future<Uint8List> downloadExport({required String jobId, required String downloadToken}) async {
    final uri = Uri.parse('$_baseUrl/v1/privacy/export/$jobId/download')
        .replace(queryParameters: {'token': downloadToken});
    final response = await _http.get(uri, headers: await _headers());
    if (response.statusCode >= 400) {
      throw PrivacyException('Download failed (${response.statusCode})');
    }
    return response.bodyBytes;
  }

  @override
  Future<void> deleteAccount({required String password}) async {
    final response = await _http.post(
      Uri.parse('$_baseUrl/v1/privacy/account/delete'),
      headers: await _headers(),
      body: jsonEncode({'password': password, 'confirm': 'DELETE'}),
    );
    if (response.statusCode >= 400) {
      throw PrivacyException(_errorFrom(response));
    }
  }

  Future<Map<String, String>> _headers() async {
    final token = await _tokens.readAccessToken();
    if (token == null) throw const PrivacyException('Not signed in');
    return {'Content-Type': 'application/json', 'Authorization': 'Bearer $token'};
  }

  Map<String, dynamic> _decode(http.Response response) {
    if (response.statusCode >= 400) {
      throw PrivacyException(_errorFrom(response));
    }
    if (response.body.isEmpty) return const {};
    return jsonDecode(response.body) as Map<String, dynamic>;
  }

  String _errorFrom(http.Response response) {
    if (response.body.isNotEmpty) {
      try {
        final error = (jsonDecode(response.body) as Map<String, dynamic>)['error'];
        if (error is String) return error;
      } on FormatException {
        // Fall through to the status-code message.
      }
    }
    return 'Request failed (${response.statusCode})';
  }
}
