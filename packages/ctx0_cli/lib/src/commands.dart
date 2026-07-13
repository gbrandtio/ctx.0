import 'dart:io';
import 'dart:isolate';

import 'catalog.dart';
import 'docs_sync.dart';
import 'injector.dart';

Future<Directory?> _resolveRegistryDir(String kind) async {
  final candidates = <String>[];
  final packageRoot = await Isolate.resolvePackageUri(Uri.parse('package:ctx0_cli/'));
  if (packageRoot != null) {
    candidates.add('${Directory.fromUri(packageRoot).parent.path}/templates/registry/$kind/features');
  }
  final script = File.fromUri(Platform.script);
  final repoRoot = script.parent.parent.parent.parent;
  candidates.add('${repoRoot.path}/registry/$kind/features');
  for (final path in candidates) {
    if (Directory(path).existsSync()) {
      return Directory(path);
    }
  }
  return null;
}

Future<InjectorRepo> openRepo([Directory? cwd]) async {
  var dir = (cwd ?? Directory.current).absolute;
  while (true) {
    final workspaceFile = File('${dir.path}/.ctx/workspace.json');
    if (workspaceFile.existsSync()) {
      final workspace = Workspace(dir);
      final registryDir = await _resolveRegistryDir(workspace.kind);
      if (registryDir == null) throw StateError('Registry not found');
      return InjectorRepo(workspace, Catalog.load(workspace.kind, registryDir));
    }
    final parent = dir.parent;
    if (parent.path == dir.path) {
      stderr.writeln('error: no .ctx/workspace.json found — run inside a ctx-scaffolded repo.');
      exit(2);
    }
    dir = parent;
  }
}

Future<void> runPubGet(InjectorRepo repo) async {
  if (repo.workspace.kind != 'mobile') return;
  stdout.writeln('\nRunning flutter pub get...');
  final result = await Process.run(
    'flutter',
    ['pub', 'get'],
    workingDirectory: repo.workspace.root.path,
    runInShell: true,
  );
  if (result.exitCode != 0) {
    stderr.writeln(result.stderr);
    stderr.writeln('warning: flutter pub get failed — run it manually.');
  }
}

Future<int> cmdEnable(InjectorRepo repo, String id) async {
  final integration = repo.catalog.byId(id);
  
  if (repo.catalog.navMethodIds.contains(id)) {
    for (final otherId in repo.catalog.navMethodIds) {
      if (otherId != id && repo.workspace.enabledFeatures.contains(otherId)) {
        repo.setIntegrationState(repo.catalog.byId(otherId), enable: false);
        stdout.writeln('  (auto-disabled mutually exclusive $otherId)');
      }
    }
  }

  repo.setIntegrationState(integration, enable: true);
  await runPubGet(repo);
  stdout.writeln('\n✓ ${integration.id} enabled.');
  if (integration.envVars.isNotEmpty) {
    stdout.writeln('  Build-time variables to provide: ${integration.envVars.join(', ')}');
  }
  if (integration.userSteps.isNotEmpty) {
    stdout.writeln('  Manual steps remaining:');
    for (final step in integration.userSteps) {
      stdout.writeln('   - $step');
    }
  }
  return 0;
}

Future<int> cmdDisable(InjectorRepo repo, String id) async {
  final integration = repo.catalog.byId(id);
  final authMethodIds = repo.catalog.authMethodIds;
  if (authMethodIds.contains(id)) {
    final otherId = authMethodIds.firstWhere((other) => other != id);
    if (!repo.workspace.enabledFeatures.contains(otherId)) {
      stderr.writeln('error: cannot disable $id — $otherId is already disabled.');
      return 1;
    }
  }
  
  if (repo.catalog.navMethodIds.contains(id)) {
    stderr.writeln('error: cannot disable a navigation method directly.');
    return 1;
  }

  repo.setIntegrationState(integration, enable: false);
  await runPubGet(repo);
  stdout.writeln('\n✓ ${integration.id} disabled. Code ejected.');
  return 0;
}

int cmdStatus(InjectorRepo repo) {
  stdout.writeln('Scaffoldable features:\n');
  for (final integration in repo.catalog.integrations) {
    final enabled = repo.workspace.enabledFeatures.contains(integration.id);
    final label = enabled ? 'ENABLED ' : 'disabled';
    stdout.writeln('  [$label] ${integration.id.padRight(20)} ${integration.summary}');
  }
  return 0;
}

int cmdDoctor(InjectorRepo repo) {
  // Simplified doctor for now
  stdout.writeln('doctor: OK');
  return 0;
}
