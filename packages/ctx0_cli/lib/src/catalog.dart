import 'dart:convert';
import 'dart:io';

/// The integration catalog, loaded from a generated repo's
/// `.ctx/integrations.json` — the SAME file the zero-dependency fallback
/// `tool/scaffold.dart` reads, so the CLI and the fallback share one
/// catalog and one marker engine and can never disagree on state.
class Catalog {
  Catalog({
    required this.kind,
    required this.authMethodIds,
    required this.navMethodIds,
    required this.securityPubspecDeps,
    required this.integrations,
  });

  final String kind;
  final Set<String> authMethodIds;
  final Set<String> navMethodIds;
  final List<String> securityPubspecDeps;
  final List<Integration> integrations;

  /// Loads the catalog from `<repoRoot>/.ctx/integrations.json`.
  factory Catalog.load(Directory repoRoot) {
    final file = File('${repoRoot.path}/.ctx/integrations.json');
    if (!file.existsSync()) {
      throw StateError(
          'no .ctx/integrations.json in ${repoRoot.path} — not a '
          'ctx-scaffolded repo, or the catalog is missing.');
    }
    final Map<String, dynamic> json;
    try {
      json = jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
    } on FormatException catch (e) {
      throw StateError('.ctx/integrations.json is not valid JSON: ${e.message}');
    }
    return Catalog(
      kind: json['kind'] as String? ?? 'unknown',
      authMethodIds: Set<String>.from(json['authMethodIds'] as List? ?? const []),
      navMethodIds: Set<String>.from(json['navMethodIds'] as List? ?? const []),
      securityPubspecDeps:
          List<String>.from(json['securityPubspecDeps'] as List? ?? const []),
      integrations: [
        for (final entry in json['integrations'] as List? ?? const [])
          Integration.fromJson(entry as Map<String, dynamic>),
      ],
    );
  }

  Integration? tryById(String id) {
    for (final i in integrations) {
      if (i.id == id) return i;
    }
    return null;
  }

  Integration byId(String id) {
    final match = tryById(id);
    if (match == null) {
      throw ArgumentError('unknown integration "$id". Known: '
          '${integrations.map((i) => i.id).join(', ')}');
    }
    return match;
  }
}

class Integration {
  const Integration({
    required this.id,
    required this.summary,
    required this.markedFiles,
    required this.sourceDirs,
    required this.testDirs,
    required this.envVars,
    required this.userSteps,
    this.providesNavTab = false,
  });

  final String id;
  final String summary;
  final bool providesNavTab;
  final List<String> markedFiles;
  final List<String> sourceDirs;
  final List<String> testDirs;
  final List<String> envVars;
  final List<String> userSteps;

  factory Integration.fromJson(Map<String, dynamic> json) => Integration(
        id: json['id'] as String,
        summary: json['summary'] as String? ?? '',
        providesNavTab: json['providesNavTab'] as bool? ?? false,
        markedFiles: List<String>.from(json['markedFiles'] as List? ?? const []),
        sourceDirs: List<String>.from(json['sourceDirs'] as List? ?? const []),
        testDirs: List<String>.from(json['testDirs'] as List? ?? const []),
        envVars: List<String>.from(json['envVars'] as List? ?? const []),
        userSteps: List<String>.from(json['userSteps'] as List? ?? const []),
      );
}
