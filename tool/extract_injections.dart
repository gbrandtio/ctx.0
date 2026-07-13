import 'dart:convert';
import 'dart:io';

const offToken = 'ctx:off';

String? commentTokenFor(String path) {
  if (path.endsWith('.yaml') || path.endsWith('.yml')) return '#';
  if (path.endsWith('.dart') ||
      path.endsWith('.kts') ||
      path.endsWith('.swift') ||
      path.endsWith('.cs')) {
    return '//';
  }
  if (path.endsWith('.xml') ||
      path.endsWith('.plist') ||
      path.endsWith('.csproj')) {
    return null;
  }
  return null;
}

void main() {
  final root = Directory.current;

  for (final kind in ['mobile', 'api']) {
    final featuresDir = Directory('${root.path}/registry/$kind/features');
    if (!featuresDir.existsSync()) continue;

    for (final featureDir in featuresDir.listSync().whereType<Directory>()) {
      final integrationFile = File('${featureDir.path}/integration.json');
      if (!integrationFile.existsSync()) continue;

      final manifest = jsonDecode(integrationFile.readAsStringSync());
      final featureId = manifest['id'];
      final markedFiles = List<String>.from(manifest['markedFiles'] ?? []);
      final injections = <String, List<String>>{};

      for (final relPath in markedFiles) {
        final file = File('${root.path}/templates/$kind/$relPath');
        if (!file.existsSync()) continue;

        final lines = file.readAsLinesSync();
        final token = commentTokenFor(relPath);
        final newLines = <String>[];
        final fileInjections = <String>[];
        
        var i = 0;
        while (i < lines.length) {
          if (lines[i].contains('ctx:$featureId:begin')) {
            newLines.add(lines[i]); // Keep begin marker
            i++;
            
            final blockLines = <String>[];
            while (i < lines.length && !lines[i].contains('ctx:$featureId:end')) {
              blockLines.add(lines[i]);
              i++;
            }
            
            // Clean block lines (remove ctx:off)
            final cleanLines = <String>[];
            for (final l in blockLines) {
              if (token == null) {
                if (l.trim() == '<!-- $offToken' || l.trim() == '$offToken -->') continue;
                cleanLines.add(l);
              } else {
                final indentMatch = RegExp(r'^\s*').firstMatch(l);
                final indent = indentMatch != null ? indentMatch.group(0)! : '';
                final rest = l.substring(indent.length);
                if (rest.startsWith('$token $offToken ')) {
                  cleanLines.add(indent + rest.substring('$token $offToken '.length));
                } else {
                  cleanLines.add(l);
                }
              }
            }
            fileInjections.add(cleanLines.join('\n'));
            if (i < lines.length) {
              newLines.add(lines[i]); // Keep end marker
            }
          } else {
            newLines.add(lines[i]);
          }
          i++;
        }
        
        injections[relPath] = fileInjections;
        file.writeAsStringSync(newLines.join('\n') + '\n');
      }

      manifest['injections'] = injections;
      integrationFile.writeAsStringSync(const JsonEncoder.withIndent('  ').convert(manifest));
    }
  }
}
