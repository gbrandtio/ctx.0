import 'dart:convert';
import 'dart:io';

/// Loads the shared wire-protocol golden vectors that the API and this client
/// both assert against. Synced into the workspace at `.ctx/vectors.json` when the
/// workspace is generated; located by walking up from the test's directory.
Map<String, dynamic> loadGoldenVectors() {
  var dir = Directory.current;
  while (true) {
    final candidate = File('${dir.path}/.ctx/vectors.json');
    if (candidate.existsSync()) {
      return jsonDecode(candidate.readAsStringSync()) as Map<String, dynamic>;
    }
    final parent = dir.parent;
    if (parent.path == dir.path) {
      throw StateError('Could not locate .ctx/vectors.json above ${Directory.current.path}');
    }
    dir = parent;
  }
}
