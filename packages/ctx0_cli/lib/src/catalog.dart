import 'dart:convert';
import 'dart:io';

class Catalog {
  Catalog({
    required this.kind,
    required this.authMethodIds,
    required this.navMethodIds,
    required this.securityPubspecDeps,
    required this.integrations,
    required this.registryRoot,
  });

  final String kind;
  final Set<String> authMethodIds;
  final Set<String> navMethodIds;
  final List<String> securityPubspecDeps;
  final List<Integration> integrations;
  final Directory registryRoot;

  factory Catalog.load(String kind, Directory registryRoot) {
    final integrations = <Integration>[];
    for (final featureDir in registryRoot.listSync().whereType<Directory>()) {
      final file = File('${featureDir.path}/integration.json');
      if (file.existsSync()) {
        final json = jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
        integrations.add(Integration.fromJson(json));
      }
    }
    
    // We need to define these somewhere, maybe read from a central catalog.json?
    // Since we removed integrations.json, we can hardcode for now or read from a manifest.
    return Catalog(
      kind: kind,
      authMethodIds: {'auth_google', 'auth_email_password'}, // Hardcoded for now
      navMethodIds: {'nav_bottom', 'nav_rail', 'nav_drawer', 'nav_none', 'nav_bottom_notched', 'nav_tabs'}, // Hardcoded
      securityPubspecDeps: ['ctx0_mobile_security'],
      integrations: integrations,
      registryRoot: registryRoot,
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
    required this.injections,
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
  final Map<String, List<String>> injections;

  factory Integration.fromJson(Map<String, dynamic> json) {
    final rawInjections = json['injections'] as Map<String, dynamic>? ?? {};
    final injections = <String, List<String>>{};
    rawInjections.forEach((key, value) {
      injections[key] = List<String>.from(value);
    });
    
    return Integration(
      id: json['id'] as String,
      summary: json['summary'] as String,
      providesNavTab: json['providesNavTab'] as bool? ?? false,
      markedFiles: List<String>.from(json['markedFiles'] as List? ?? []),
      sourceDirs: List<String>.from(json['sourceDirs'] as List? ?? []),
      testDirs: List<String>.from(json['testDirs'] as List? ?? []),
      envVars: List<String>.from(json['envVars'] as List? ?? []),
      userSteps: List<String>.from(json['userSteps'] as List? ?? []),
      injections: injections,
    );
  }
}
