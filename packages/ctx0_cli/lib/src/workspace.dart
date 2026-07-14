import 'dart:convert';
import 'dart:io';

import 'commands.dart';
import 'create.dart';
import 'docs_sync.dart';
import 'injector.dart';

/// A workspace is a directory with `.ctx/workspace.json` holding one
/// mobile repo and one API repo generated with the same name — the
/// signing headers and wire protocol match by construction, and
/// enable/disable/doctor fan out to both sides.
Directory? findWorkspace([Directory? cwd]) {
  var dir = (cwd ?? Directory.current).absolute;
  while (true) {
    final file = File('${dir.path}/.ctx/workspace.json');
    if (file.existsSync()) {
      final text = file.readAsStringSync();
      if (!text.contains('"kind": "mobile"') && !text.contains('"kind": "api"')) {
        return dir;
      }
    }
    final parent = dir.parent;
    if (parent.path == dir.path) return null;
    dir = parent;
  }
}

Future<List<InjectorRepo>> workspaceRepos(Directory workspace) async {
  final repos = <InjectorRepo>[];
  for (final entity in workspace.listSync().whereType<Directory>()) {
    if (File('${entity.path}/.ctx/workspace.json').existsSync()) {
      repos.add(await openRepo(entity));
    }
  }
  return repos;
}

Future<int> createWorkspace({
  required String name,
  required String org,
  required List<String> withIntegrations,
  required Directory outDir,
  required bool localPackages,
  String? templateDirFlag,
}) async {
  final mobileTemplate =
      await resolveTemplateDir('mobile', templateDirFlag);
  final apiTemplate = await resolveTemplateDir('api', templateDirFlag);
  if (mobileTemplate == null || apiTemplate == null) {
    stderr.writeln('error: could not locate the mobile and api templates. '
        'Pass --template-dir or set CTX_TEMPLATES.');
    return 2;
  }
  if (outDir.existsSync() && outDir.listSync().isNotEmpty) {
    stderr.writeln('error: ${outDir.path} exists and is not empty.');
    return 2;
  }

  final appResult = await createApp(
    name: name,
    org: org,
    withIntegrations: withIntegrations,
    templateDir: mobileTemplate,
    outDir: Directory('${outDir.path}/mobile'),
    localPackages: localPackages,
  );
  if (appResult != 0) return appResult;

  final apiResult = await createApi(
    name: name,
    org: org,
    withIntegrations: withIntegrations,
    templateDir: apiTemplate,
    outDir: Directory('${outDir.path}/api'),
    localPackages: localPackages,
  );
  if (apiResult != 0) return apiResult;

  Directory('${outDir.path}/.ctx').createSync(recursive: true);
  File('${outDir.path}/.ctx/workspace.json').writeAsStringSync(
      '${const JsonEncoder.withIndent('  ').convert({
        'name': name,
        'org': org,
        'cliVersion': cliVersion,
        'createdAt': DateTime.now().toUtc().toIso8601String(),
      })}\n');

  stdout.writeln('\n✓ workspace $name created: ${outDir.path}/mobile + '
      '${outDir.path}/api (signing headers and wire protocol aligned).');
  stdout.writeln('  Run `ctx0 doctor` here to verify both sides + the '
      'protocol lock.');
  return 0;
}

/// Fan-out enable/disable: applies to every workspace repo whose catalog
/// defines the id.
Future<int> workspaceToggle(
    Directory workspace, String id, {required bool enable}) async {
  var applied = 0;
  var exitCode = 0;
  for (final repo in await workspaceRepos(workspace)) {
    if (!repo.catalog.integrations.any((i) => i.id == id)) continue;
    applied++;
    stdout.writeln('--- ${repo.root.path} (${repo.catalog.kind})');
    final result = enable
        ? await cmdEnable(repo, id)
        : await cmdDisable(repo, id);
    if (result != 0) exitCode = result;
  }
  if (applied == 0) {
    stderr.writeln('error: no repo in this workspace defines "$id".');
    return 2;
  }
  return exitCode;
}

Future<int> workspaceDoctor(Directory workspace) async {
  var exitCode = 0;
  final repos = await workspaceRepos(workspace);
  for (final repo in repos) {
    stdout.writeln('--- ${repo.root.path} (${repo.catalog.kind})');
    if (cmdDoctor(repo) != 0) exitCode = 1;
  }

  // ---- Wire-protocol lock: the mobile and API security packages ship
  // a protocol.txt (independent package versions; the protocol is the
  // compatibility contract). ----
  final mobile = repos.where((r) => r.catalog.kind == 'mobile').firstOrNull;
  final api = repos.where((r) => r.catalog.kind == 'api').firstOrNull;
  if (mobile != null && api != null) {
    final mobileProtocol = _packageProtocol(mobilePackageDirs(mobile, quiet: true)
        ?.values.firstOrNull);
    final apiProtocol = _packageProtocol(apiPackageDirs(api, quiet: true)
        ?['Ctx0.Security']);
    if (mobileProtocol == null || apiProtocol == null) {
      stdout.writeln('protocol lock: skipped '
          '(mobile=$mobileProtocol api=$apiProtocol — resolve dependencies '
          'first).');
    } else if (_majorMinor(mobileProtocol) != _majorMinor(apiProtocol)) {
      stderr.writeln('✗ protocol lock: ctx0_mobile_security speaks protocol '
          '$mobileProtocol but Ctx0.Security speaks $apiProtocol — the two '
          'sides use different wire protocols. Align the package versions.');
      exitCode = 1;
    } else {
      stdout.writeln('protocol lock: OK (protocol $mobileProtocol on both '
          'sides).');
    }
  }
  return exitCode;
}

String? _packageProtocol(Directory? packageDir) {
  if (packageDir == null) return null;
  final file = File('${packageDir.path}/protocol.txt');
  return file.existsSync() ? file.readAsStringSync().trim() : null;
}

String _majorMinor(String version) {
  final parts = version.split('.');
  return parts.length >= 2 ? '${parts[0]}.${parts[1]}' : version;
}
