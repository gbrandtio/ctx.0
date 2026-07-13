import 'dart:convert';
import 'dart:io';

/// A generated repo's integration catalog (`.ctx/integrations.json`) —
/// the shared data source between the `ctx0` CLI and the template's
/// legacy `tool/scaffold.dart`. The catalog travels WITH the generated
/// repo so future CLI versions can keep scaffolding it.
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

  factory Catalog.load(Directory root) {
    final file = File('${root.path}/.ctx/integrations.json');
    if (!file.existsSync()) {
      throw StateError('no .ctx/integrations.json in ${root.path} — not a '
          'ctx-scaffolded repo (or run from the repo root).');
    }
    final json = jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
    return Catalog(
      kind: json['kind'] as String? ?? 'mobile',
      authMethodIds: Set<String>.from(json['authMethodIds'] as List? ?? []),
      navMethodIds: Set<String>.from(json['navMethodIds'] as List? ?? []),
      securityPubspecDeps:
          List<String>.from(json['securityPubspecDeps'] as List? ?? []),
      integrations: [
        for (final entry in json['integrations'] as List)
          Integration.fromJson(entry as Map<String, dynamic>),
      ],
    );
  }

  Integration byId(String id) {
    final match = integrations.where((i) => i.id == id);
    if (match.isEmpty) {
      throw StateError('unknown integration "$id". Known: '
          '${integrations.map((i) => i.id).join(', ')}');
    }
    return match.first;
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
        summary: json['summary'] as String,
        providesNavTab: json['providesNavTab'] as bool? ?? false,
        markedFiles: List<String>.from(json['markedFiles'] as List),
        sourceDirs: List<String>.from(json['sourceDirs'] as List),
        testDirs: List<String>.from(json['testDirs'] as List),
        envVars: List<String>.from(json['envVars'] as List),
        userSteps: List<String>.from(json['userSteps'] as List),
      );
}
