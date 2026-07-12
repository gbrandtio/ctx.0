// Integration scaffolder (docs/INTEGRATIONS.md).
//
// The ONLY supported way to wire an optional integration in or out:
//
//   dart run tool/scaffold.dart status
//   dart run tool/scaffold.dart enable  <integration-id>
//   dart run tool/scaffold.dart disable <integration-id>
//   dart run tool/scaffold.dart doctor
//
// It toggles `ctx:<id>:begin/end` marker blocks across pubspec.yaml,
// lib/app/modules.dart and the platform config files, keeps the analyzer
// excludes in sync, and parks the integration's tests while disabled.
// Zero dependencies by design: it must run right after `git clone`,
// before `flutter pub get`.
//
// Security controls (RASP, request signing, ALE, secure storage) are NOT
// integrations and have no entry here — `doctor` asserts they are intact.

import 'dart:io';

// ---------------------------------------------------------------------------
// Manifest: every optional integration and the exact files it touches.
// To add a new integration: add marker blocks to the touched files, then
// describe them here. Keep ids kebab/snake case; they appear in markers.
// ---------------------------------------------------------------------------

class Integration {
  const Integration({
    required this.id,
    required this.summary,
    required this.markedFiles,
    required this.sourceDirs,
    required this.testDirs,
    required this.envVars,
    required this.userSteps,
  });

  final String id;
  final String summary;

  /// Files containing `ctx:<id>:begin` / `ctx:<id>:end` marker blocks.
  final List<String> markedFiles;

  /// Dart source dirs compiled only while enabled; excluded from analysis
  /// while disabled (their vendor imports would not resolve).
  final List<String> sourceDirs;

  /// Test dirs whose *.dart files are renamed to *.dart.off while
  /// disabled, so `flutter test` does not try to compile them.
  final List<String> testDirs;

  /// Environment variables the integration consumes
  /// (docs/ENVIRONMENT_VARIABLES.md).
  final List<String> envVars;

  /// Steps only a human can perform (consoles, dashboards, key material).
  final List<String> userSteps;
}

const integrations = [
  Integration(
    id: 'maps_google',
    summary: 'Google Map + nearby geo-tagged items (MapsModule)',
    markedFiles: [
      'pubspec.yaml',
      'lib/app/modules.dart',
      'android/app/src/main/AndroidManifest.xml',
      'ios/Runner/AppDelegate.swift',
      'ios/Runner/Info.plist',
    ],
    sourceDirs: ['lib/features/maps'],
    testDirs: ['test/features/maps'],
    envVars: ['MAPS_API_KEY'],
    userSteps: [
      'Create a Google Maps API key (Maps SDK for Android + iOS) at '
          'https://console.cloud.google.com and pass it as MAPS_API_KEY '
          '(docs/ENVIRONMENT_VARIABLES.md).',
    ],
  ),
  Integration(
    id: 'push_firebase',
    summary: 'FCM push + in-app notification feed (NotificationsModule)',
    markedFiles: [
      'pubspec.yaml',
      'lib/app/modules.dart',
      'android/app/src/main/AndroidManifest.xml',
    ],
    sourceDirs: ['lib/features/notifications'],
    testDirs: ['test/features/notifications'],
    envVars: [],
    userSteps: [
      'Create a Firebase project and register the Android/iOS apps, then '
          'either run `flutterfire configure` or place '
          'android/app/google-services.json and '
          'ios/Runner/GoogleService-Info.plist manually.',
      'Enable FCM dispatch on the API side '
          '(api-template/docs — notifications feature).',
    ],
  ),
  Integration(
    id: 'payments_stripe',
    summary: 'Stripe PaymentSheet checkout (PaymentsModule)',
    markedFiles: [
      'pubspec.yaml',
      'lib/app/modules.dart',
      'android/app/build.gradle.kts',
    ],
    sourceDirs: ['lib/features/payments'],
    testDirs: ['test/features/payments'],
    envVars: [
      'STRIPE_PUBLISHABLE_KEY',
      'APPLE_PAY_MERCHANT_ID',
      'MERCHANT_COUNTRY_CODE',
    ],
    userSteps: [
      'Set STRIPE_PUBLISHABLE_KEY from https://dashboard.stripe.com '
          '(publishable key only — the secret key lives on the API).',
      'Configure the Stripe secret key + webhook endpoint on the API side '
          '(api-template/docs/features/PAYMENTS_STRIPE.md).',
    ],
  ),
];

/// Security-plane invariants `doctor` enforces. These are never
/// toggleable; see docs/INTEGRATIONS.md §1 and docs/SECURITY.md.
const securityPubspecDeps = [
  'freerasp',
  'pointycastle',
  'flutter_secure_storage',
];

// ---------------------------------------------------------------------------
// Marker-block engine
// ---------------------------------------------------------------------------

const offToken = 'ctx:off';

/// Line-comment token per file type; XML-ish files use wrapper lines
/// instead because XML has no line comments.
String? commentTokenFor(String path) {
  if (path.endsWith('.yaml') || path.endsWith('.yml')) return '#';
  if (path.endsWith('.dart') || path.endsWith('.kts') || path.endsWith('.swift')) {
    return '//';
  }
  if (path.endsWith('.xml') || path.endsWith('.plist')) return null;
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
  final off = content
      .where((l) => l.trimLeft().startsWith('$token $offToken '))
      .length;
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
          .where((l) =>
              l.trim() != '<!-- $offToken' && l.trim() != '$offToken -->')
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
// File operations
// ---------------------------------------------------------------------------

final root = _findRoot();

Directory _findRoot() {
  var dir = Directory.current;
  while (true) {
    if (File('${dir.path}/pubspec.yaml').existsSync() &&
        File('${dir.path}/tool/scaffold.dart').existsSync()) {
      return dir;
    }
    final parent = dir.parent;
    if (parent.path == dir.path) {
      stderr.writeln('error: run from within mobile-template/');
      exit(2);
    }
    dir = parent;
  }
}

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
  _setTestsParked(integration, parked: !enable);
  _syncAnalyzerExcludes();
}

void _setTestsParked(Integration integration, {required bool parked}) {
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
void _syncAnalyzerExcludes() {
  final file = fileAt('analysis_options.yaml');
  var lines = file.readAsLinesSync();
  final begin =
      lines.indexWhere((l) => l.contains('ctx:integration-excludes:begin'));
  final end =
      lines.indexWhere((l) => l.contains('ctx:integration-excludes:end'));
  if (begin == -1 || end == -1 || end < begin) {
    throw StateError('integration-excludes markers missing in '
        'analysis_options.yaml');
  }
  final excludes = <String>[];
  for (final integration in integrations) {
    if (currentState(integration) == BlockState.disabled) {
      for (final dir in [...integration.sourceDirs, ...integration.testDirs]) {
        excludes.add('    - $dir/**');
      }
    }
  }
  lines = [...lines.sublist(0, begin + 1), ...excludes, ...lines.sublist(end)];
  file.writeAsStringSync('${lines.join('\n')}\n');
}

/// The integration's state as told by its pubspec block (the block that
/// controls what actually gets compiled and linked).
BlockState currentState(Integration integration) {
  final lines = fileAt('pubspec.yaml').readAsLinesSync();
  final blocks = findBlocks(lines, integration.id);
  return blockState(lines, blocks.first, '#');
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

Integration integrationById(String id) {
  final match = integrations.where((i) => i.id == id);
  if (match.isEmpty) {
    stderr.writeln('error: unknown integration "$id". Known: '
        '${integrations.map((i) => i.id).join(', ')}');
    exit(2);
  }
  return match.first;
}

Future<void> runPubGet() async {
  stdout.writeln('\nRunning flutter pub get...');
  final result = await Process.run(
    'flutter',
    ['pub', 'get'],
    workingDirectory: root.path,
    runInShell: true,
  );
  if (result.exitCode != 0) {
    stderr.writeln(result.stderr);
    stderr.writeln('warning: flutter pub get failed — run it manually.');
  }
}

Future<void> cmdEnable(String id) async {
  final integration = integrationById(id);
  setIntegrationState(integration, enable: true);
  await runPubGet();
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
  stdout.writeln('  Verify: dart run tool/scaffold.dart doctor '
      '&& flutter analyze && flutter test');
}

Future<void> cmdDisable(String id) async {
  final integration = integrationById(id);
  setIntegrationState(integration, enable: false);
  await runPubGet();
  stdout.writeln('\n✓ ${integration.id} disabled. Its code stays in the '
      'tree (unreferenced, excluded from analysis); the vendor SDK is no '
      'longer a dependency.');
  stdout.writeln('  Verify: dart run tool/scaffold.dart doctor '
      '&& flutter analyze && flutter test');
}

void cmdStatus() {
  stdout.writeln('Optional integrations (docs/INTEGRATIONS.md):\n');
  for (final integration in integrations) {
    final state = currentState(integration);
    final label = switch (state) {
      BlockState.enabled => 'ENABLED ',
      BlockState.disabled => 'disabled',
      _ => 'DRIFTED ',
    };
    stdout.writeln('  [$label] ${integration.id.padRight(16)} '
        '${integration.summary}');
  }
  stdout.writeln('\nSecurity plane (RASP, signing, ALE): permanent, '
      'not listed — see docs/SECURITY.md.');
}

int cmdDoctor() {
  final problems = <String>[];

  for (final integration in integrations) {
    final states = <String, BlockState>{};
    for (final path in integration.markedFiles) {
      final lines = fileAt(path).readAsLinesSync();
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

    final enabled = currentState(integration) == BlockState.enabled;
    for (final dirPath in integration.testDirs) {
      final dir = Directory('${root.path}/$dirPath');
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

  // Security plane: never weakened, never toggleable
  // (docs/INTEGRATIONS.md §1, docs/SECURITY.md).
  final pubspec = fileAt('pubspec.yaml').readAsStringSync();
  for (final dep in securityPubspecDeps) {
    final active = RegExp('^  $dep:', multiLine: true).hasMatch(pubspec);
    if (!active) {
      problems.add('security: dependency "$dep" missing or commented out '
          'in pubspec.yaml');
    }
  }
  final main = fileAt('lib/main.dart').readAsStringSync();
  final raspIndex = main.indexOf('RaspService().init()');
  final moduleInitIndex = main.indexOf('module.init()');
  if (raspIndex == -1) {
    problems.add('security: RaspService().init() missing from lib/main.dart');
  } else if (moduleInitIndex != -1 && raspIndex > moduleInitIndex) {
    problems.add('security: RaspService().init() must run before module '
        'init in lib/main.dart');
  }
  final factory =
      fileAt('lib/data/services/api/api_service_factory.dart')
          .readAsStringSync();
  for (final interceptor in ['SecureDeviceSigningClient', 'AleClient']) {
    if (!factory.contains(interceptor)) {
      problems.add('security: $interceptor missing from the interceptor '
          'chain (api_service_factory.dart)');
    }
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

Future<void> main(List<String> args) async {
  if (args.isEmpty) {
    stdout.writeln('usage: dart run tool/scaffold.dart '
        '<status|doctor|enable <id>|disable <id>>');
    exit(2);
  }
  switch (args.first) {
    case 'status':
      cmdStatus();
    case 'doctor':
      exit(cmdDoctor());
    case 'enable' when args.length == 2:
      await cmdEnable(args[1]);
    case 'disable' when args.length == 2:
      await cmdDisable(args[1]);
    default:
      stderr.writeln('error: unrecognized command "${args.join(' ')}"');
      exit(2);
  }
}
