import 'dart:convert';
import 'dart:io';

void main() {
  final root = Directory.current;
  final registryDir = Directory('${root.path}/registry');
  if (registryDir.existsSync()) registryDir.deleteSync(recursive: true);
  registryDir.createSync();

  for (final kind in ['mobile', 'api']) {
    final templateDir = Directory('${root.path}/templates/$kind');
    final integrationsFile = File('${templateDir.path}/.ctx/integrations.json');
    if (!integrationsFile.existsSync()) continue;

    final catalog = jsonDecode(integrationsFile.readAsStringSync());
    final featuresDir = Directory('${registryDir.path}/$kind/features');
    featuresDir.createSync(recursive: true);

    for (final integration in catalog['integrations']) {
      final id = integration['id'];
      if (id.startsWith('nav_')) continue; // Navs are not extracted as folders yet

      final featureDir = Directory('${featuresDir.path}/$id');
      featureDir.createSync();

      // Write feature manifest
      final manifest = {
        'id': id,
        'summary': integration['summary'],
        'providesNavTab': integration['providesNavTab'],
        'markedFiles': integration['markedFiles'],
        'sourceDirs': integration['sourceDirs'],
        'testDirs': integration['testDirs'],
        'envVars': integration['envVars'],
        'userSteps': integration['userSteps'],
      };
      File('${featureDir.path}/integration.json').writeAsStringSync(
          const JsonEncoder.withIndent('  ').convert(manifest));

      // Move source dirs
      for (final src in integration['sourceDirs']) {
        final srcDir = Directory('${templateDir.path}/$src');
        if (srcDir.existsSync()) {
          final dest = Directory('${featureDir.path}/$src');
          dest.createSync(recursive: true);
          _copyTree(srcDir, dest);
          srcDir.deleteSync(recursive: true);
        }
      }

      // Move test dirs
      for (final src in integration['testDirs']) {
        final srcDir = Directory('${templateDir.path}/$src');
        if (srcDir.existsSync()) {
          final dest = Directory('${featureDir.path}/$src');
          dest.createSync(recursive: true);
          _copyTree(srcDir, dest);
          srcDir.deleteSync(recursive: true);
        }
      }
    }
  }
}

void _copyTree(Directory from, Directory to) {
  for (final entity in from.listSync(followLinks: false)) {
    final basename = entity.uri.pathSegments.lastWhere((s) => s.isNotEmpty);
    if (entity is Directory) {
      final newDir = Directory('${to.path}/$basename')..createSync(recursive: true);
      _copyTree(entity, newDir);
    } else if (entity is File) {
      entity.copySync('${to.path}/$basename');
    }
  }
}
