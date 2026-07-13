import 'dart:convert';
import 'dart:io';

void main() {
  final root = Directory.current;

  for (final kind in ['mobile', 'api']) {
    final registryDir = Directory('${root.path}/registry/$kind/features');
    if (!registryDir.existsSync()) continue;

    for (final featureDir in registryDir.listSync().whereType<Directory>()) {
      final integrationFile = File('${featureDir.path}/integration.json');
      if (!integrationFile.existsSync()) continue;

      final manifest = jsonDecode(integrationFile.readAsStringSync());
      final injections = manifest['injections'] ?? {};
      final sourceDirs = List<String>.from(manifest['sourceDirs'] ?? []);
      final testDirs = List<String>.from(manifest['testDirs'] ?? []);
      
      // Inject code
      for (final path in injections.keys) {
        final file = File('${root.path}/templates/$kind/$path');
        if (!file.existsSync()) continue;
        
        final lines = file.readAsLinesSync();
        final newLines = <String>[];
        final snippets = List<String>.from(injections[path]);
        var snippetIdx = 0;
        
        var i = 0;
        while (i < lines.length) {
          if (lines[i].contains('ctx:${manifest['id']}:begin')) {
            newLines.add(lines[i]);
            if (snippetIdx < snippets.length) {
              if (snippets[snippetIdx].trim().isNotEmpty) {
                newLines.add(snippets[snippetIdx]);
              }
              snippetIdx++;
            }
            i++;
            while (i < lines.length && !lines[i].contains('ctx:${manifest['id']}:end')) {
              i++;
            }
            if (i < lines.length) {
              newLines.add(lines[i]);
            }
          } else {
            newLines.add(lines[i]);
          }
          i++;
        }
        file.writeAsStringSync(newLines.join('\n') + '\n');
      }

      // Copy dirs back
      for (final dir in sourceDirs) {
        final srcDir = Directory('${featureDir.path}/$dir');
        if (srcDir.existsSync()) {
          final destDir = Directory('${root.path}/templates/$kind/$dir');
          _copyTree(srcDir, destDir);
        }
      }
      for (final dir in testDirs) {
        final srcDir = Directory('${featureDir.path}/$dir');
        if (srcDir.existsSync()) {
          final destDir = Directory('${root.path}/templates/$kind/$dir');
          _copyTree(srcDir, destDir);
        }
      }
    }
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
