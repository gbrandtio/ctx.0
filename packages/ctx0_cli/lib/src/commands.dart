import 'dart:io';

import 'catalog.dart';
import 'doctor.dart';
import 'injector.dart';
import 'markers.dart';

/// Walks up from [cwd] to the nearest generated repo (`.ctx/workspace.json`)
/// and loads its catalog from `.ctx/integrations.json`.
Future<InjectorRepo> openRepo([Directory? cwd]) async {
  var dir = (cwd ?? Directory.current).absolute;
  while (true) {
    if (File('${dir.path}/.ctx/workspace.json').existsSync()) {
      final workspace = Workspace(dir);
      return InjectorRepo(workspace, Catalog.load(dir));
    }
    final parent = dir.parent;
    if (parent.path == dir.path) {
      stderr.writeln('error: no .ctx/workspace.json found — run inside a '
          'ctx-scaffolded repo.');
      exit(2);
    }
    dir = parent;
  }
}

Future<void> runPubGet(InjectorRepo repo) async {
  if (repo.workspace.kind != 'mobile') return;
  stdout.writeln('\nRunning flutter pub get...');
  final result = await Process.run('flutter', ['pub', 'get'],
      workingDirectory: repo.workspace.root.path, runInShell: true);
  if (result.exitCode != 0) {
    stderr.writeln(result.stderr);
    stderr.writeln('warning: flutter pub get failed — run it manually.');
  }
}

Future<int> cmdEnable(InjectorRepo repo, String id) async {
  final Integration integration;
  try {
    integration = repo.catalog.byId(id);
  } on ArgumentError catch (e) {
    stderr.writeln('error: ${e.message}');
    return 2;
  }

  if (repo.isEnabled(integration)) {
    stdout.writeln('✓ ${integration.id} is already enabled.');
    return 0;
  }

  // Navigation methods are mutually exclusive: enabling one disables the
  // other active one.
  if (repo.catalog.navMethodIds.contains(id)) {
    for (final otherId in repo.catalog.navMethodIds) {
      final other = repo.catalog.tryById(otherId);
      if (other != null && otherId != id && repo.isEnabled(other)) {
        repo.setIntegrationState(other, enable: false);
        stdout.writeln('  (auto-disabled mutually exclusive $otherId)');
      }
    }
  }

  repo.setIntegrationState(integration, enable: true);
  await runPubGet(repo);
  stdout.writeln('\n✓ ${integration.id} enabled.');
  if (integration.envVars.isNotEmpty) {
    stdout.writeln('  Build-time variables to provide: '
        '${integration.envVars.join(', ')}');
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
  final Integration integration;
  try {
    integration = repo.catalog.byId(id);
  } on ArgumentError catch (e) {
    stderr.writeln('error: ${e.message}');
    return 2;
  }

  if (!repo.isEnabled(integration)) {
    stdout.writeln('✓ ${integration.id} is already disabled.');
    return 0;
  }

  // Auth core invariant: at least one sign-in method must stay enabled.
  if (repo.catalog.authMethodIds.contains(id)) {
    final others = repo.catalog.authMethodIds.where((o) => o != id);
    final anyOtherEnabled = others.any((o) {
      final other = repo.catalog.tryById(o);
      return other != null && repo.isEnabled(other);
    });
    if (!anyOtherEnabled) {
      stderr.writeln('error: cannot disable $id — it is the last enabled '
          'sign-in method; the app must keep at least one.');
      return 1;
    }
  }

  // Nav methods can only be switched by enabling another (never left with
  // none), so refuse a direct disable.
  if (repo.catalog.navMethodIds.contains(id)) {
    stderr.writeln('error: cannot disable a navigation method directly — '
        'enable a different nav method instead.');
    return 1;
  }

  repo.setIntegrationState(integration, enable: false);
  await runPubGet(repo);
  stdout.writeln('\n✓ ${integration.id} disabled. Its code stays in the tree '
      '(commented out / excluded); the vendor SDK is no longer a dependency.');
  return 0;
}

int cmdStatus(InjectorRepo repo) {
  stdout.writeln('Scaffoldable features:\n');
  for (final integration in repo.catalog.integrations) {
    final label = switch (repo.currentState(integration)) {
      BlockState.enabled => 'ENABLED ',
      BlockState.disabled => 'disabled',
      _ => 'DRIFTED ',
    };
    stdout.writeln('  [$label] ${integration.id.padRight(20)} '
        '${integration.summary}');
  }
  return 0;
}

int cmdDoctor(InjectorRepo repo) => runDoctor(repo);
