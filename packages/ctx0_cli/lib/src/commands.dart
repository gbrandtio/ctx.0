import 'dart:io';

import 'catalog.dart';
import 'docs_sync.dart';
import 'markers.dart';

/// enable/disable/status/doctor against the ctx-scaffolded repo that
/// contains [cwd] (found via `.ctx/integrations.json`).
MarkerRepo openRepo([Directory? cwd]) {
  var dir = (cwd ?? Directory.current).absolute;
  while (true) {
    if (File('${dir.path}/.ctx/integrations.json').existsSync()) {
      return MarkerRepo(dir, Catalog.load(dir));
    }
    final parent = dir.parent;
    if (parent.path == dir.path) {
      stderr.writeln('error: no .ctx/integrations.json found — run inside a '
          'ctx-scaffolded repo.');
      exit(2);
    }
    dir = parent;
  }
}

Future<void> runPubGet(MarkerRepo repo) async {
  if (repo.catalog.kind != 'mobile') return;
  stdout.writeln('\nRunning flutter pub get...');
  final result = await Process.run(
    'flutter',
    ['pub', 'get'],
    workingDirectory: repo.root.path,
    runInShell: true,
  );
  if (result.exitCode != 0) {
    stderr.writeln(result.stderr);
    stderr.writeln('warning: flutter pub get failed — run it manually.');
  }
}

Future<int> cmdEnable(MarkerRepo repo, String id) async {
  final integration = repo.catalog.byId(id);
  repo.setIntegrationState(integration, enable: true);
  await runPubGet(repo);
  stdout.writeln('\n✓ ${integration.id} enabled.');
  if (integration.envVars.isNotEmpty) {
    stdout.writeln('  Build-time variables to provide '
        '(docs/ENVIRONMENT_VARIABLES.md): ${integration.envVars.join(', ')}');
  }
  if (integration.userSteps.isNotEmpty) {
    stdout.writeln('  Manual steps remaining (a human must do these):');
    for (final step in integration.userSteps) {
      stdout.writeln('   - $step');
    }
  }
  stdout.writeln('  Verify: ctx0 doctor && flutter analyze && flutter test');
  return 0;
}

Future<int> cmdDisable(MarkerRepo repo, String id) async {
  final integration = repo.catalog.byId(id);
  final authMethodIds = repo.catalog.authMethodIds;
  if (authMethodIds.contains(id)) {
    final otherId = authMethodIds.firstWhere((other) => other != id);
    if (repo.currentState(repo.catalog.byId(otherId)) != BlockState.enabled) {
      stderr.writeln('error: cannot disable $id — $otherId is already '
          'disabled and the app must keep at least one sign-in method '
          '(docs/INTEGRATIONS.md §1).');
      return 1;
    }
  }
  repo.setIntegrationState(integration, enable: false);
  await runPubGet(repo);
  stdout.writeln('\n✓ ${integration.id} disabled. Its code stays in the '
      'tree (unreferenced, excluded from analysis); the vendor SDK is no '
      'longer a dependency.');
  stdout.writeln('  Verify: ctx0 doctor && flutter analyze && flutter test');
  return 0;
}

int cmdStatus(MarkerRepo repo) {
  stdout.writeln('Scaffoldable features (docs/INTEGRATIONS.md):\n');
  for (final integration in repo.catalog.integrations) {
    final state = repo.currentState(integration);
    final label = switch (state) {
      BlockState.enabled => 'ENABLED ',
      BlockState.disabled => 'disabled',
      _ => 'DRIFTED ',
    };
    stdout.writeln('  [$label] ${integration.id.padRight(20)} '
        '${integration.summary}');
  }
  stdout.writeln('\nSecurity plane (RASP, signing, ALE) and the auth core '
      '(AuthBloc, token lifecycle, logout): permanent, not listed — see '
      'docs/SECURITY.md. At least one auth method must stay enabled.');
  return 0;
}

int cmdDoctor(MarkerRepo repo) {
  final problems = <String>[];
  final catalog = repo.catalog;

  for (final integration in catalog.integrations) {
    final states = <String, BlockState>{};
    for (final path in integration.markedFiles) {
      final lines = repo.fileAt(path).readAsLinesSync();
      final token = commentTokenFor(path);
      final blocks = findBlocks(lines, integration.id);
      if (blocks.isEmpty) {
        problems.add('${integration.id}: no marker blocks in $path');
        continue;
      }
      for (final block in blocks) {
        final state = blockState(lines, block, token);
        if (state == BlockState.mixed) {
          problems.add('${integration.id}: half-toggled block in $path '
              '(line ${block.start + 1})');
        }
        states[path] = state;
      }
    }
    final distinct = states.values.toSet()..remove(BlockState.empty);
    if (distinct.length > 1) {
      problems.add('${integration.id}: inconsistent state across files: '
          '${states.entries.map((e) => '${e.key}=${e.value.name}').join(', ')}');
    }

    final enabled = repo.currentState(integration) == BlockState.enabled;
    for (final dirPath in integration.testDirs) {
      final dir = Directory('${repo.root.path}/$dirPath');
      if (!dir.existsSync()) continue;
      for (final entity in dir.listSync(recursive: true).whereType<File>()) {
        if (enabled && entity.path.endsWith('.dart.off')) {
          problems.add('${integration.id}: parked test '
              '${entity.path} but integration is enabled');
        }
        if (!enabled && entity.path.endsWith('.dart')) {
          problems.add('${integration.id}: live test ${entity.path} '
              'but integration is disabled');
        }
      }
    }
  }

  // Auth core invariant: at least one sign-in method stays enabled.
  if (catalog.authMethodIds.isNotEmpty &&
      catalog.authMethodIds.every(
          (id) => repo.currentState(catalog.byId(id)) != BlockState.enabled)) {
    problems.add('auth: all sign-in methods are disabled; enable one of '
        '${catalog.authMethodIds.join(', ')}');
  }

  if (catalog.integrations.where((i) => i.providesNavTab).isNotEmpty &&
      catalog.integrations
          .where((i) => i.providesNavTab)
          .every((i) => repo.currentState(i) != BlockState.enabled)) {
    stdout.writeln('note: no enabled feature contributes a bottom-nav tab; '
        'the shell will boot to the splash route until a product module '
        'provides one (docs/APP_SHELL.md §5).');
  }

  if (catalog.kind == 'mobile') {
    problems.addAll(_mobileSecurityChecks(repo));
  }
  if (catalog.kind == 'api') {
    problems.addAll(_apiSecurityChecks(repo));
  }
  problems.addAll(docsDriftProblems(repo));

  if (problems.isEmpty) {
    stdout.writeln('doctor: OK — marker blocks consistent, security plane '
        'intact.');
    return 0;
  }
  stderr.writeln('doctor: ${problems.length} problem(s):');
  for (final problem in problems) {
    stderr.writeln('  ✗ $problem');
  }
  return 1;
}

/// Security plane: never weakened, never toggleable
/// (docs/INTEGRATIONS.md §1, docs/SECURITY.md).
List<String> _mobileSecurityChecks(MarkerRepo repo) {
  final problems = <String>[];
  final pubspec = repo.fileAt('pubspec.yaml').readAsStringSync();
  for (final dep in repo.catalog.securityPubspecDeps) {
    final active = RegExp('^  $dep:', multiLine: true).hasMatch(pubspec);
    if (!active) {
      problems.add('security: dependency "$dep" missing or commented out '
          'in pubspec.yaml');
    }
  }
  final main = repo.fileAt('lib/main.dart').readAsStringSync();
  if (!main.contains('buildSecurityConfig()')) {
    problems.add('security: buildSecurityConfig() missing from lib/main.dart '
        '(lib/app/security_bootstrap.dart is the only bridge between app '
        'constants and the security plane)');
  }
  final raspIndex = main.indexOf('RaspService(');
  final moduleInitIndex = main.indexOf('module.init()');
  if (raspIndex == -1) {
    problems.add('security: RaspService(...).init() missing from '
        'lib/main.dart');
  } else if (moduleInitIndex != -1 && raspIndex > moduleInitIndex) {
    problems.add('security: RaspService(...).init() must run before module '
        'init in lib/main.dart');
  }
  if (!main.contains('ApiServiceFactory(')) {
    problems.add('security: ApiServiceFactory missing from lib/main.dart — '
        'the interceptor chain (ctx0_mobile_security) is not wired');
  }
  if (RegExp(r'dependency_overrides:[\s\S]*ctx0_mobile_security:')
      .hasMatch(pubspec)) {
    problems.add('security: ctx0_mobile_security must not be overridden via '
        'dependency_overrides (docs/INTEGRATIONS.md §1)');
  }
  if (Directory('${repo.root.path}/lib/data/services/security').existsSync() ||
      Directory('${repo.root.path}/lib/data/services/api/interceptors')
          .existsSync()) {
    problems.add('security: vendored copy of the security plane found under '
        'lib/data/services — the plane ships only as ctx0_mobile_security');
  }
  return problems;
}

/// API security plane: the Ctx0.Security packages wired through the
/// single seams (AddAppSecurity/UseAppSecurity → AddCtxSecurity/
/// UseCtxSecurity) and both EF interceptors on the DbContext.
List<String> _apiSecurityChecks(MarkerRepo repo) {
  final problems = <String>[];
  final infra = _firstExisting(repo, [
    'Infrastructure/Infrastructure.csproj',
  ]);
  if (infra == null) {
    problems.add('security: Infrastructure csproj not found');
    return problems;
  }
  final csproj = infra.readAsStringSync();
  if (!csproj.contains('Ctx0.Security')) {
    problems.add('security: Infrastructure csproj does not reference the '
        'Ctx0.Security packages');
  }

  final programs = repo.root
      .listSync(recursive: true)
      .whereType<File>()
      .where((f) => f.path.endsWith('Program.cs'))
      .toList();
  final program = programs.isEmpty ? '' : programs.first.readAsStringSync();
  if (!program.contains('AddAppSecurity') &&
      !program.contains('AddCtxSecurity')) {
    problems.add('security: AddAppSecurity/AddCtxSecurity missing from '
        'Program.cs');
  }
  if (!program.contains('UseAppSecurity') &&
      !program.contains('UseCtxSecurity')) {
    problems.add('security: UseAppSecurity/UseCtxSecurity missing from '
        'Program.cs — the security pipeline is not wired');
  }

  final di = repo.root
      .listSync(recursive: true)
      .whereType<File>()
      .where((f) => f.path.endsWith('ServiceCollectionExtensions.cs'))
      .toList();
  final diText = di.isEmpty ? '' : di.first.readAsStringSync();
  for (final interceptor in ['RlsInterceptor', 'EnvelopeEncryptionInterceptor']) {
    if (!diText.contains(interceptor)) {
      problems.add('security: $interceptor not registered on the DbContext '
          '(ServiceCollectionExtensions.cs)');
    }
  }
  return problems;
}

File? _firstExisting(MarkerRepo repo, List<String> paths) {
  for (final path in paths) {
    final f = repo.fileAt(path);
    if (f.existsSync()) return f;
  }
  return null;
}
