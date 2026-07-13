import 'dart:convert';
import 'dart:io';
import 'catalog.dart';

class Workspace {
  Workspace(this.root) {
    file = File('${root.path}/.ctx/workspace.json');
    if (file.existsSync()) {
      final data = jsonDecode(file.readAsStringSync());
      kind = data['kind'];
      enabledFeatures = Set<String>.from(data['enabledFeatures'] ?? []);
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
    file.writeAsStringSync(const JsonEncoder.withIndent('  ').convert({
      'kind': kind,
      'enabledFeatures': enabledFeatures.toList()..sort(),
    }) + '\n');
  }
}

class InjectorRepo {
  InjectorRepo(this.workspace, this.catalog);

  final Workspace workspace;
  final Catalog catalog;

  Directory get root => workspace.root;

  File fileAt(String relative) => File('${workspace.root.path}/$relative');

  void setIntegrationState(Integration integration, {required bool enable}) {
    if (enable) {
      if (workspace.enabledFeatures.contains(integration.id)) return;
      _copyDirs(integration.sourceDirs, integration);
      _copyDirs(integration.testDirs, integration);
      _injectCode(integration);
      workspace.enabledFeatures.add(integration.id);
    } else {
      if (!workspace.enabledFeatures.contains(integration.id)) return;
      _removeDirs(integration.sourceDirs);
      _removeDirs(integration.testDirs);
      _ejectCode(integration);
      workspace.enabledFeatures.remove(integration.id);
    }
    workspace.save();
  }

  void _copyDirs(List<String> dirs, Integration integration) {
    for (final dir in dirs) {
      final srcDir = Directory('${catalog.registryRoot.path}/${integration.id}/$dir');
      if (!srcDir.existsSync()) continue;
      final destDir = Directory('${workspace.root.path}/$dir');
      _copyTree(srcDir, destDir);
    }
  }

  void _removeDirs(List<String> dirs) {
    for (final dir in dirs) {
      final destDir = Directory('${workspace.root.path}/$dir');
      if (destDir.existsSync()) destDir.deleteSync(recursive: true);
    }
  }

  void _copyTree(Directory from, Directory to) {
    to.createSync(recursive: true);
    for (final entity in from.listSync(followLinks: false)) {
      final basename = entity.uri.pathSegments.lastWhere((s) => s.isNotEmpty);
      if (entity is Directory) {
        _copyTree(entity, Directory('${to.path}/$basename'));
      } else if (entity is File) {
        entity.copySync('${to.path}/$basename');
      }
    }
  }

  void _injectCode(Integration integration) {
    for (final entry in integration.injections.entries) {
      final path = entry.key;
      final snippets = entry.value;
      final file = fileAt(path);
      if (!file.existsSync()) continue;
      
      final lines = file.readAsLinesSync();
      final newLines = <String>[];
      var snippetIdx = 0;
      
      var i = 0;
      while (i < lines.length) {
        if (lines[i].contains('ctx:${integration.id}:begin')) {
          newLines.add(lines[i]);
          if (snippetIdx < snippets.length) {
            newLines.add(snippets[snippetIdx]);
            snippetIdx++;
          }
          i++;
          while (i < lines.length && !lines[i].contains('ctx:${integration.id}:end')) {
            i++; // skip any existing content inside
          }
          if (i < lines.length) {
            newLines.add(lines[i]); // end marker
          }
        } else {
          newLines.add(lines[i]);
        }
        i++;
      }
      file.writeAsStringSync(newLines.join('\n') + '\n');
    }
  }

  void _ejectCode(Integration integration) {
    for (final entry in integration.injections.entries) {
      final path = entry.key;
      final file = fileAt(path);
      if (!file.existsSync()) continue;
      
      final lines = file.readAsLinesSync();
      final newLines = <String>[];
      
      var i = 0;
      while (i < lines.length) {
        if (lines[i].contains('ctx:${integration.id}:begin')) {
          newLines.add(lines[i]);
          i++;
          while (i < lines.length && !lines[i].contains('ctx:${integration.id}:end')) {
            i++; // drop injected content
          }
          if (i < lines.length) {
            newLines.add(lines[i]); // end marker
          }
        } else {
          newLines.add(lines[i]);
        }
        i++;
      }
      file.writeAsStringSync(newLines.join('\n') + '\n');
    }
  }
}

extension InjectorRepoRoot on InjectorRepo {
  Directory get root => workspace.root;
}
