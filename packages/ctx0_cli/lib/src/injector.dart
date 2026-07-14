import 'dart:convert';
import 'dart:io';

import 'catalog.dart';
import 'markers.dart';

/// A generated repo (`.ctx/workspace.json` present). `enabledFeatures` is a
/// cache of the marker state; the marker blocks in the tree are the source
/// of truth, and [doctor] reconciles the two.
class Workspace {
  Workspace(this.root) {
    file = File('${root.path}/.ctx/workspace.json');
    if (file.existsSync()) {
      final data = jsonDecode(file.readAsStringSync()) as Map<String, dynamic>;
      kind = data['kind'] as String? ?? 'unknown';
      enabledFeatures = Set<String>.from(data['enabledFeatures'] ?? const []);
    } else {
      kind = 'unknown';
      enabledFeatures = {};
    }
  }

  final Directory root;
  late final File file;
  late String kind;
  late Set<String> enabledFeatures;

  void save() {
    if (!file.parent.existsSync()) file.parent.createSync(recursive: true);
    file.writeAsStringSync('${const JsonEncoder.withIndent('  ').convert({
          'kind': kind,
          'enabledFeatures': enabledFeatures.toList()..sort(),
        })}\n');
  }
}

class InjectorRepo {
  InjectorRepo(this.workspace, this.catalog);

  final Workspace workspace;
  final Catalog catalog;

  Directory get root => workspace.root;

  File fileAt(String relative) => File('${workspace.root.path}/$relative');

  /// The integration's state, read from its first marked file that carries
  /// a NON-EMPTY block (some features have an empty pubspec/csproj block —
  /// e.g. no vendor deps — which says nothing about the feature's state).
  BlockState currentState(Integration integration) {
    for (final path in integration.markedFiles) {
      final file = fileAt(path);
      if (!file.existsSync()) continue;
      final lines = file.readAsLinesSync();
      final blocks = findBlocks(lines, integration.id);
      for (final block in blocks) {
        final state = blockState(lines, block, commentTokenFor(path));
        if (state != BlockState.empty) return state;
      }
    }
    return BlockState.empty;
  }

  bool isEnabled(Integration integration) =>
      currentState(integration) == BlockState.enabled;

  /// Comment-toggle every marker block for [integration], park/unpark its
  /// tests, and (mobile) resync the analyzer excludes — identical to the
  /// fallback engine, so state never diverges.
  void setIntegrationState(Integration integration, {required bool enable}) {
    for (final path in integration.markedFiles) {
      final file = fileAt(path);
      if (!file.existsSync()) continue;
      final token = commentTokenFor(path);
      var lines = file.readAsLinesSync();
      final blocks = findBlocks(lines, integration.id);
      // Transform bottom-up so earlier indexes stay valid.
      for (final block in blocks.reversed) {
        lines = transformBlock(lines, block, token, enable: enable);
      }
      file.writeAsStringSync('${lines.join('\n')}\n');
    }
    _setTestsParked(integration, parked: !enable);
    if (workspace.kind == 'mobile') _syncAnalyzerExcludes();

    if (enable) {
      workspace.enabledFeatures.add(integration.id);
    } else {
      workspace.enabledFeatures.remove(integration.id);
    }
    workspace.save();
  }

  void _setTestsParked(Integration integration, {required bool parked}) {
    for (final dirPath in integration.testDirs) {
      final dir = Directory('${workspace.root.path}/$dirPath');
      if (!dir.existsSync()) continue;
      for (final entity in dir.listSync(recursive: true).whereType<File>()) {
        final path = entity.path;
        if (parked && path.endsWith('.dart')) {
          entity.renameSync('$path.off');
        } else if (!parked && path.endsWith('.dart.off')) {
          entity.renameSync(path.substring(0, path.length - 4));
        }
      }
    }
  }

  /// Rewrites the managed exclude list in analysis_options.yaml to cover
  /// exactly the currently-disabled integrations (mobile only).
  void _syncAnalyzerExcludes() {
    final file = fileAt('analysis_options.yaml');
    if (!file.existsSync()) return;
    var lines = file.readAsLinesSync();
    final begin =
        lines.indexWhere((l) => l.contains('ctx:integration-excludes:begin'));
    final end =
        lines.indexWhere((l) => l.contains('ctx:integration-excludes:end'));
    if (begin == -1 || end == -1 || end < begin) return;
    final excludes = <String>[];
    for (final integration in catalog.integrations) {
      if (currentState(integration) == BlockState.disabled) {
        for (final dir in [...integration.sourceDirs, ...integration.testDirs]) {
          excludes.add('    - $dir/**');
        }
      }
    }
    lines = [...lines.sublist(0, begin + 1), ...excludes, ...lines.sublist(end)];
    file.writeAsStringSync('${lines.join('\n')}\n');
  }

  /// Seeds `enabledFeatures` from the actual marker state in the tree —
  /// used at create time so a fresh workspace reports the truth.
  void syncEnabledFromMarkers() {
    workspace.enabledFeatures
      ..clear()
      ..addAll(catalog.integrations
          .where(isEnabled)
          .map((i) => i.id));
    workspace.save();
  }
}
