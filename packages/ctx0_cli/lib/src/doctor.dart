import 'dart:io';

import 'docs_sync.dart';
import 'injector.dart';
import 'markers.dart';

/// Real integrity check (replaces the old stub): marker-block consistency,
/// parked-test state, the auth-core invariant, docs drift, and the
/// security-plane invariants. Parameterized by repo kind so it runs on both
/// the Flutter app and the .NET API.
int runDoctor(InjectorRepo repo) {
  final problems = <String>[];

  // ---- Per-integration marker + test consistency ----
  for (final integration in repo.catalog.integrations) {
    final states = <String, BlockState>{};
    for (final path in integration.markedFiles) {
      final file = repo.fileAt(path);
      if (!file.existsSync()) {
        problems.add('${integration.id}: marked file missing: $path');
        continue;
      }
      final lines = file.readAsLinesSync();
      final token = commentTokenFor(path);
      final List<Block> blocks;
      try {
        blocks = findBlocks(lines, integration.id);
      } on StateError catch (e) {
        problems.add('${integration.id}: ${e.message} in $path');
        continue;
      }
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

    final enabled = repo.isEnabled(integration);
    for (final dirPath in integration.testDirs) {
      final dir = Directory('${repo.root.path}/$dirPath');
      if (!dir.existsSync()) continue;
      for (final entity in dir.listSync(recursive: true).whereType<File>()) {
        if (enabled && entity.path.endsWith('.dart.off')) {
          problems.add('${integration.id}: parked test ${entity.path} but '
              'integration is enabled');
        }
        if (!enabled && entity.path.endsWith('.dart')) {
          problems.add('${integration.id}: live test ${entity.path} but '
              'integration is disabled');
        }
      }
    }
  }

  // ---- Auth core: at least one sign-in method enabled ----
  if (repo.catalog.authMethodIds.isNotEmpty &&
      repo.catalog.authMethodIds.every((id) {
        final i = repo.catalog.tryById(id);
        return i == null || !repo.isEnabled(i);
      })) {
    problems.add('auth: no sign-in method is enabled; enable one of '
        '${repo.catalog.authMethodIds.join(', ')}');
  }

  // ---- Docs drift (version headers vs installed packages) ----
  problems.addAll(docsDriftProblems(repo));

  // ---- Security plane ----
  if (repo.workspace.kind == 'mobile') {
    problems.addAll(_mobileSecurityProblems(repo));
  } else if (repo.workspace.kind == 'api') {
    problems.addAll(_apiSecurityProblems(repo));
  }

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

List<String> _mobileSecurityProblems(InjectorRepo repo) {
  final problems = <String>[];
  final pubspecFile = repo.fileAt('pubspec.yaml');
  if (!pubspecFile.existsSync()) return ['security: pubspec.yaml missing'];
  final pubspec = pubspecFile.readAsStringSync();
  for (final dep in repo.catalog.securityPubspecDeps) {
    if (!RegExp('^  $dep:', multiLine: true).hasMatch(pubspec)) {
      problems.add('security: dependency "$dep" missing or commented out in '
          'pubspec.yaml');
    }
  }
  final mainFile = repo.fileAt('lib/main.dart');
  final main = mainFile.existsSync() ? mainFile.readAsStringSync() : '';
  if (!main.contains('buildSecurityConfig()')) {
    problems.add('security: buildSecurityConfig() missing from lib/main.dart');
  }
  final raspIndex = main.indexOf('RaspService(');
  final moduleInitIndex = main.indexOf('module.init()');
  if (raspIndex == -1) {
    problems.add('security: RaspService(...).init() missing from lib/main.dart');
  } else if (moduleInitIndex != -1 && raspIndex > moduleInitIndex) {
    problems.add('security: RaspService(...).init() must run before module '
        'init in lib/main.dart');
  }
  if (!main.contains('ApiServiceFactory(')) {
    problems.add('security: ApiServiceFactory missing from lib/main.dart — the '
        'interceptor chain is not wired');
  }
  if (RegExp(r'dependency_overrides:[\s\S]*ctx0_mobile_security:')
      .hasMatch(pubspec)) {
    problems.add('security: ctx0_mobile_security must not be overridden via '
        'dependency_overrides');
  }
  if (Directory('${repo.root.path}/lib/data/services/security').existsSync() ||
      Directory('${repo.root.path}/lib/data/services/api/interceptors')
          .existsSync()) {
    problems.add('security: vendored copy of the security plane found under '
        'lib/data/services — the plane ships only as ctx0_mobile_security');
  }
  return problems;
}

List<String> _apiSecurityProblems(InjectorRepo repo) {
  final problems = <String>[];
  // The Ctx0.Security packages must remain referenced (Package/Project
  // reference) by the Infrastructure project.
  final sep = Platform.pathSeparator;
  final referencesSecurity = Directory(repo.root.path)
      .listSync(recursive: true, followLinks: false)
      .whereType<File>()
      .where((f) => f.path.endsWith('.csproj'))
      .where((f) =>
          !f.path.contains('${sep}obj$sep') && !f.path.contains('${sep}bin$sep'))
      .any((f) => f.readAsStringSync().contains('Ctx0.Security'));
  if (!referencesSecurity) {
    problems.add('security: no project references Ctx0.Security — the server '
        'security plane is not wired');
  }
  return problems;
}
