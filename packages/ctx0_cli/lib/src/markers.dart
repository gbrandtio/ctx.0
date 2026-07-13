import 'dart:io';

import 'catalog.dart';

/// The marker-block engine, ported verbatim from the mobile template's
/// `tool/scaffold.dart` (docs/INTEGRATIONS.md). Everything optional is
/// wrapped in `ctx:<id>:begin/end` blocks; enabling/disabling comments or
/// uncomments the block content, keeps the analyzer excludes in sync, and
/// parks disabled integrations' tests as `*.dart.off`.
const offToken = 'ctx:off';

/// Line-comment token per file type; XML-ish files use wrapper lines
/// instead because XML has no line comments.
String? commentTokenFor(String path) {
  if (path.endsWith('.yaml') || path.endsWith('.yml')) return '#';
  if (path.endsWith('.dart') ||
      path.endsWith('.kts') ||
      path.endsWith('.swift') ||
      path.endsWith('.cs')) {
    return '//';
  }
  // XML-family files (incl. .csproj) have no line comments: blocks are
  // wrapped in `<!-- ctx:off` ... `ctx:off -->` lines instead.
  if (path.endsWith('.xml') ||
      path.endsWith('.plist') ||
      path.endsWith('.csproj')) {
    return null;
  }
  throw StateError('No comment style known for $path');
}

class Block {
  Block(this.start, this.end); // line indexes of begin/end marker lines
  final int start;
  final int end;
}

List<Block> findBlocks(List<String> lines, String id) {
  final blocks = <Block>[];
  int? start;
  for (var i = 0; i < lines.length; i++) {
    if (lines[i].contains('ctx:$id:begin')) {
      if (start != null) {
        throw StateError('Nested ctx:$id:begin at line ${i + 1}');
      }
      start = i;
    } else if (lines[i].contains('ctx:$id:end')) {
      if (start == null) {
        throw StateError('ctx:$id:end without begin at line ${i + 1}');
      }
      blocks.add(Block(start, i));
      start = null;
    }
  }
  if (start != null) throw StateError('Unclosed ctx:$id:begin');
  return blocks;
}

enum BlockState { enabled, disabled, mixed, empty }

BlockState blockState(List<String> lines, Block block, String? token) {
  final content = lines
      .sublist(block.start + 1, block.end)
      .where((l) => l.trim().isNotEmpty)
      .toList();
  if (content.isEmpty) return BlockState.empty;
  if (token == null) {
    // XML: disabled iff wrapped in `<!-- ctx:off` ... `ctx:off -->` lines.
    return content.first.trim() == '<!-- $offToken'
        ? BlockState.disabled
        : BlockState.enabled;
  }
  final off =
      content.where((l) => l.trimLeft().startsWith('$token $offToken ')).length;
  if (off == 0) return BlockState.enabled;
  if (off == content.length) return BlockState.disabled;
  return BlockState.mixed;
}

List<String> transformBlock(
  List<String> lines,
  Block block,
  String? token, {
  required bool enable,
}) {
  final before = lines.sublist(0, block.start + 1);
  final content = lines.sublist(block.start + 1, block.end);
  final after = lines.sublist(block.end);

  List<String> next;
  if (token == null) {
    if (enable) {
      next = content
          .where(
              (l) => l.trim() != '<!-- $offToken' && l.trim() != '$offToken -->')
          .toList();
    } else {
      final indent = RegExp(r'^\s*').firstMatch(content.first)!.group(0)!;
      next = ['$indent<!-- $offToken', ...content, '$indent$offToken -->'];
    }
  } else {
    next = content.map((l) {
      if (l.trim().isEmpty) return l;
      final indent = RegExp(r'^\s*').firstMatch(l)!.group(0)!;
      final rest = l.substring(indent.length);
      if (enable) {
        return rest.startsWith('$token $offToken ')
            ? indent + rest.substring('$token $offToken '.length)
            : l;
      }
      return rest.startsWith('$token $offToken ')
          ? l
          : '$indent$token $offToken $rest';
    }).toList();
  }
  return [...before, ...next, ...after];
}

// ---------------------------------------------------------------------------
// Repo operations (parameterized by root)
// ---------------------------------------------------------------------------

class MarkerRepo {
  MarkerRepo(this.root, this.catalog);

  final Directory root;
  final Catalog catalog;

  File fileAt(String relative) => File('${root.path}/$relative');

  void setIntegrationState(Integration integration, {required bool enable}) {
    for (final path in integration.markedFiles) {
      final file = fileAt(path);
      final token = commentTokenFor(path);
      var lines = file.readAsLinesSync();
      final blocks = findBlocks(lines, integration.id);
      if (blocks.isEmpty) {
        throw StateError('No ctx:${integration.id} markers in $path');
      }
      // Transform from the bottom so earlier indexes stay valid.
      for (final block in blocks.reversed) {
        lines = transformBlock(lines, block, token, enable: enable);
      }
      file.writeAsStringSync('${lines.join('\n')}\n');
    }
    setTestsParked(integration, parked: !enable);
    syncAnalyzerExcludes();
  }

  void setTestsParked(Integration integration, {required bool parked}) {
    for (final dirPath in integration.testDirs) {
      final dir = Directory('${root.path}/$dirPath');
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
  /// exactly the currently-disabled integrations.
  void syncAnalyzerExcludes() {
    final file = fileAt('analysis_options.yaml');
    if (!file.existsSync()) return; // API repos have no analyzer excludes
    var lines = file.readAsLinesSync();
    final begin =
        lines.indexWhere((l) => l.contains('ctx:integration-excludes:begin'));
    final end =
        lines.indexWhere((l) => l.contains('ctx:integration-excludes:end'));
    if (begin == -1 || end == -1 || end < begin) {
      throw StateError(
          'integration-excludes markers missing in analysis_options.yaml');
    }
    final excludes = <String>[];
    for (final integration in catalog.integrations) {
      if (currentState(integration) == BlockState.disabled) {
        for (final dir in [
          ...integration.sourceDirs,
          ...integration.testDirs
        ]) {
          excludes.add('    - $dir/**');
        }
      }
    }
    lines = [
      ...lines.sublist(0, begin + 1),
      ...excludes,
      ...lines.sublist(end)
    ];
    file.writeAsStringSync('${lines.join('\n')}\n');
  }

  /// The integration's state as told by its first marked file — pubspec
  /// for anything with vendor packages, lib sources for pure-Dart
  /// features.
  BlockState currentState(Integration integration) {
    final path = integration.markedFiles.first;
    final lines = fileAt(path).readAsLinesSync();
    final blocks = findBlocks(lines, integration.id);
    return blockState(lines, blocks.first, commentTokenFor(path));
  }
}
