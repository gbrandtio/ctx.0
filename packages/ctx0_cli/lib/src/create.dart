import 'dart:convert';
import 'dart:io';
import 'dart:isolate';

import 'commands.dart';

const cliVersion = '0.1.0';

/// Directory names never copied into a generated project.
const _skipDirs = {
  '.git',
  'build',
  '.dart_tool',
  '.idea',
  'Pods',
  '.symlinks',
  'ephemeral',
  'DerivedData',
  'bin',
  'obj',
};

/// `ctx0 create app <name>` — materialize the mobile template as a new
/// product repo: copy, parameterize the `App` placeholder, point the
/// security plane at hosted packages, record `.ctx/manifest.json`, and
/// apply the requested integrations.
Future<int> createApp({
  required String name,
  required String org,
  required List<String> withIntegrations,
  required Directory templateDir,
  required Directory outDir,
  required bool localPackages,
}) async {
  if (!RegExp(r'^[a-z][a-z0-9_]*$').hasMatch(name)) {
    stderr.writeln('error: <name> must be a snake_case Dart identifier '
        '(got "$name").');
    return 2;
  }
  if (!RegExp(r'^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$').hasMatch(org)) {
    stderr.writeln('error: --org must be a reverse-DNS id like com.acme '
        '(got "$org").');
    return 2;
  }
  if (outDir.existsSync() && outDir.listSync().isNotEmpty) {
    stderr.writeln('error: ${outDir.path} exists and is not empty.');
    return 2;
  }
  // No integrations.json check here anymore as it lives in the registry

  final pascal = _pascal(name);
  final camel = _camel(name);

  stdout.writeln('Creating $name from ${templateDir.path} ...');
  _copyTree(templateDir, outDir);

  // ---- Parameterize the `App` placeholder (longest match first) ----
  final replacements = <String, String>{
    'com.example.appTemplate': '$org.$camel',
    'com.example.app_template': '$org.$name',
    'app_template': name,
    'X-App-Device-Id': 'X-$pascal-Device-Id',
    'X-App-Signature': 'X-$pascal-Signature',
  };
  _rewriteTree(outDir, replacements);

  // ---- Security plane: hosted package unless --local-packages ----
  final pubspec = File('${outDir.path}/pubspec.yaml');
  var pubspecText = pubspec.readAsStringSync();
  final pathDep = RegExp(
      r'ctx0_mobile_security:\n    path: [^\n]+');
  if (localPackages) {
    final local =
        _findUp(templateDir, 'packages/ctx0_mobile_security')?.path;
    if (local == null) {
      stderr.writeln('error: --local-packages requires a ctx.0 checkout '
          '(packages/ctx0_mobile_security not found above the template).');
      return 2;
    }
    pubspecText = pubspecText.replaceFirst(
        pathDep, 'ctx0_mobile_security:\n    path: $local');
  } else {
    pubspecText = pubspecText.replaceFirst(
        pathDep, 'ctx0_mobile_security: ^0.2.0');
  }
  pubspec.writeAsStringSync(pubspecText);

  // ---- Workspace & Manifest ----
  File('${outDir.path}/.ctx/workspace.json').writeAsStringSync(
      '{"kind": "mobile", "enabledFeatures": []}\n');
  File('${outDir.path}/.ctx/manifest.json').writeAsStringSync(
      '${const JsonEncoder.withIndent('  ').convert({
        'kind': 'mobile',
        'name': name,
        'org': org,
        'cliVersion': cliVersion,
        'createdAt': DateTime.now().toUtc().toIso8601String(),
        'deviceIdHeader': 'X-$pascal-Device-Id',
        'signatureHeader': 'X-$pascal-Signature',
      })}\n');

  // ---- Seed workspace state from the template's actual marker state, then
  // apply the requested integrations (comment-toggle, same engine as the
  // fallback) so `ctx0 status`/`disable` tell the truth on a fresh app. ----
  final repo = await openRepo(outDir);
  repo.syncEnabledFromMarkers();
  for (final id in withIntegrations) {
    final integration = repo.catalog.tryById(id);
    if (integration == null) {
      stderr.writeln('  warning: unknown integration "$id" — skipped.');
      continue;
    }
    if (!repo.isEnabled(integration)) {
      repo.setIntegrationState(integration, enable: true);
    }
    stdout.writeln('  enabled ${integration.id}');
  }

  stdout.writeln('\n✓ $name created at ${outDir.path}');
  stdout.writeln('''
Next steps:
  1. cd ${outDir.path} && flutter pub get && flutter test
  2. ctx0 status                     # see what is on/off
  3. ctx0 doctor                     # verify integrity
  4. Fill in docs/core-business/ and point your agent at AGENTS.md.
  5. The signing headers are X-$pascal-* — configure the API side's
     Security:Ale:DeviceIdHeader / SignatureHeader to match.''');
  final userSteps = [
    for (final id in withIntegrations)
      ...?repo.catalog.tryById(id)?.userSteps,
  ];
  if (userSteps.isNotEmpty) {
    stdout.writeln('\nManual steps for the enabled integrations:');
    for (final step in userSteps) {
      stdout.writeln('  - $step');
    }
  }
  return 0;
}

/// Locates the template payload. Order: --template-dir flag,
/// CTX_TEMPLATES env var, then the repo-relative layout when the CLI runs
/// from a ctx.0 checkout (path activation / dart run). Packaged template
/// archives replace this at publish time (tool/pack_templates.dart).
String? _envTemplate(String kind) {
  final env = Platform.environment['CTX_TEMPLATES'];
  return env == null ? null : '$env/$kind';
}

Future<Directory?> resolveTemplateDir(String kind, String? flagValue) async {
  // Repo layout relative to this script: packages/ctx0_cli/bin/ctx.dart.
  // Preferred over the embedded payload when running from a ctx.0 checkout,
  // so a stale packed payload can never silently shadow the live templates.
  final script = File.fromUri(Platform.script);
  final repoTemplate = '${script.parent.parent.parent.parent.path}/templates/$kind';
  // Embedded payload of the installed CLI package (packed at publish by
  // tool/pack_templates.dart).
  String? embedded;
  final packageRoot =
      await Isolate.resolvePackageUri(Uri.parse('package:ctx0_cli/'));
  if (packageRoot != null) {
    embedded = '${Directory.fromUri(packageRoot).parent.path}/templates/$kind';
  }
  final candidates = <String>[
    ?flagValue,
    ?_envTemplate(kind),
    repoTemplate,
    ?embedded,
  ];
  for (final path in candidates) {
    if (Directory('$path/.ctx').existsSync()) {
      stdout.writeln('Using $kind template: $path');
      return Directory(path);
    }
  }
  return null;
}

void _copyTree(Directory from, Directory to) {
  to.createSync(recursive: true);
  for (final entity in from.listSync(followLinks: false)) {
    final basename = entity.uri.pathSegments.lastWhere((s) => s.isNotEmpty);
    if (entity is Directory) {
      if (_skipDirs.contains(basename)) continue;
      _copyTree(entity, Directory('${to.path}/$basename'));
    } else if (entity is File) {
      if (basename == '.DS_Store') continue;
      entity.copySync('${to.path}/$basename');
    }
  }
}

void _rewriteTree(Directory root, Map<String, String> replacements) {
  for (final entity
      in root.listSync(recursive: true, followLinks: false).whereType<File>()) {
    final String content;
    try {
      content = entity.readAsStringSync();
    } on FileSystemException {
      continue; // binary
    }
    var next = content;
    replacements.forEach((from, replacement) {
      next = next.replaceAll(from, replacement);
    });
    if (!identical(next, content) && next != content) {
      entity.writeAsStringSync(next);
    }
  }
}

/// `ctx0 create api <name>` — materialize the API template: copy,
/// parameterize the `App` placeholder (`AppApi`, `AppDbContext`,
/// `App.sln`, `X-App-*` headers), and point the security plane at the
/// hosted Ctx0.Security NuGets. DB-side names (app_user roles,
/// app.current_user_id) are protocol constants of Ctx0.Security.EfCore
/// and are NOT renamed.
Future<int> createApi({
  required String name,
  required String org,
  required List<String> withIntegrations,
  required Directory templateDir,
  required Directory outDir,
  required bool localPackages,
}) async {
  if (!RegExp(r'^[a-z][a-z0-9_]*$').hasMatch(name)) {
    stderr.writeln('error: <name> must be a snake_case identifier '
        '(got "$name").');
    return 2;
  }
  if (outDir.existsSync() && outDir.listSync().isNotEmpty) {
    stderr.writeln('error: ${outDir.path} exists and is not empty.');
    return 2;
  }
  // No integrations.json check here anymore as it lives in the registry

  final pascal = _pascal(name);

  stdout.writeln('Creating $name API from ${templateDir.path} ...');
  _copyTree(templateDir, outDir);

  // ---- Security plane: hosted NuGets unless --local-packages ----
  final packagesDir = _findUp(templateDir, 'packages/dotnet')?.path ?? '';
  if (localPackages && packagesDir.isEmpty) {
    stderr.writeln('error: --local-packages requires a ctx.0 checkout '
        '(packages/dotnet not found above the template).');
    return 2;
  }
  final projectRef = RegExp(
      r'<ProjectReference Include="[^"]*packages[\\/]dotnet[\\/]'
      r'(Ctx0\.Security[^\\/]*)[\\/][^"]*" />');
  for (final entity in outDir
      .listSync(recursive: true, followLinks: false)
      .whereType<File>()
      .where((f) => f.path.endsWith('.csproj'))) {
    var text = entity.readAsStringSync();
    text = text.replaceAllMapped(
        projectRef,
        (m) => localPackages
            ? '<ProjectReference Include="$packagesDir/${m[1]}/${m[1]}.csproj" />'
            : '<PackageReference Include="${m[1]}" Version="0.2.0" />');
    entity.writeAsStringSync(text);
  }

  // ---- Solution file: the package projects exist only in a ctx.0
  // checkout; hosted consumers reference NuGets instead. ----
  for (final sln in outDir
      .listSync(recursive: true, followLinks: false)
      .whereType<File>()
      .where((f) => f.path.endsWith('.sln'))) {
    _fixSolution(sln, packagesDir, localPackages: localPackages);
  }

  // ---- Parameterize the `App` placeholder ----
  _rewriteTree(outDir, {
    'AppApi': '${pascal}Api',
    'AppDbContext': '${pascal}DbContext',
    'App.sln': '$pascal.sln',
    'X-App-Device-Id': 'X-$pascal-Device-Id',
    'X-App-Signature': 'X-$pascal-Signature',
  });
  _renamePaths(outDir, {
    'AppApi': '${pascal}Api',
    'AppDbContext': '${pascal}DbContext',
    'App.sln': '$pascal.sln',
  });

  // ---- Workspace & Manifest ----
  File('${outDir.path}/.ctx/workspace.json').writeAsStringSync(
      '{"kind": "api", "enabledFeatures": []}\n');
  File('${outDir.path}/.ctx/manifest.json').writeAsStringSync(
      '${const JsonEncoder.withIndent('  ').convert({
        'kind': 'api',
        'name': name,
        'org': org,
        'cliVersion': cliVersion,
        'createdAt': DateTime.now().toUtc().toIso8601String(),
        'deviceIdHeader': 'X-$pascal-Device-Id',
        'signatureHeader': 'X-$pascal-Signature',
      })}\n');

  // ---- Seed workspace state from marker state, then apply --with ids
  // idempotently (unknown ids are silently ignored so a workspace can pass
  // one list to both sides). ----
  final repo = await openRepo(outDir);
  repo.syncEnabledFromMarkers();
  for (final id in withIntegrations) {
    final integration = repo.catalog.tryById(id);
    if (integration != null && !repo.isEnabled(integration)) {
      repo.setIntegrationState(integration, enable: true);
    }
  }

  stdout.writeln('\n✓ $name API created at ${outDir.path}');
  stdout.writeln('''
Next steps:
  1. cd ${outDir.path} && dotnet build && dotnet test
  2. ctx0 status / ctx0 doctor
  3. The signing headers are X-$pascal-* (appsettings Security:Ale) —
     they already match a mobile app generated with the same name.
  4. Fill in docs and point your agent at AGENTS.md.''');
  return 0;
}

/// Renames files/directories whose basename contains a placeholder
/// (deepest paths first so parents stay valid).
void _renamePaths(Directory root, Map<String, String> replacements) {
  final entities = root.listSync(recursive: true, followLinks: false)
    ..sort((a, b) => b.path.length.compareTo(a.path.length));
  for (final entity in entities) {
    final segments = entity.path.split(Platform.pathSeparator);
    var basename = segments.last;
    replacements.forEach((from, replacement) {
      basename = basename.replaceAll(from, replacement);
    });
    if (basename != segments.last) {
      entity.renameSync(
          '${segments.sublist(0, segments.length - 1).join(Platform.pathSeparator)}'
          '${Platform.pathSeparator}$basename');
    }
  }
}

String _pascal(String snake) => snake
    .split('_')
    .map((w) => w.isEmpty ? w : w[0].toUpperCase() + w.substring(1))
    .join();

String _camel(String snake) {
  final pascal = _pascal(snake);
  return pascal.isEmpty ? pascal : pascal[0].toLowerCase() + pascal.substring(1);
}

/// Drops (hosted) or re-points (--local-packages) the Ctx0.Security
/// package projects in a generated solution file.
void _fixSolution(File sln, String packagesDir, {required bool localPackages}) {
  final lines = sln.readAsLinesSync();
  final projectLine = RegExp(
      r'Project\(.*\) = "(Ctx0\.Security[^"]*)", "([^"]*)", "\{([^}]+)\}"');
  final guids = <String>{};
  final out = <String>[];
  var skipUntilEndProject = false;
  for (final line in lines) {
    if (skipUntilEndProject) {
      if (line.trim() == 'EndProject') skipUntilEndProject = false;
      continue;
    }
    final match = projectLine.firstMatch(line);
    if (match != null && match.group(2)!.contains('Ctx0.Security')) {
      if (localPackages) {
        final name = match.group(1)!;
        out.add(line.replaceFirst(match.group(2)!,
            '$packagesDir/$name/$name.csproj'.replaceAll('/', r'\')));
        continue;
      }
      guids.add(match.group(3)!);
      skipUntilEndProject = true;
      continue;
    }
    if (guids.any(line.contains)) continue;
    out.add(line);
  }
  sln.writeAsStringSync('${out.join('\n')}\n');
}

/// Walks up from [start] until `<dir>/<probe>` exists.
Directory? _findUp(Directory start, String probe) {
  var dir = start.absolute;
  while (true) {
    final candidate = Directory('${dir.path}/$probe');
    if (candidate.existsSync()) return candidate;
    final parent = dir.parent;
    if (parent.path == dir.path) return null;
    dir = parent;
  }
}
