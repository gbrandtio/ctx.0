import 'dart:io';

import 'package:args/args.dart';
import 'package:ctx0_cli/src/commands.dart';
import 'package:ctx0_cli/src/create.dart';
import 'package:ctx0_cli/src/docs_sync.dart';
import 'package:ctx0_cli/src/upgrade.dart';
import 'package:ctx0_cli/src/workspace.dart';

const _usage = '''
ctx0 — the ctx.0 scaffolder

Usage:
  ctx0 create app <name>        [--org com.acme] [--with id1,id2] [--out dir]
  ctx0 create api <name>        [--org com.acme] [--with id1,id2] [--out dir]
  ctx0 create workspace <name>  [--org com.acme] [--with id1,id2] [--out dir]
                               (all: [--template-dir dir] [--local-packages])
  ctx0 status                   # what is on/off in this repo
  ctx0 enable <integration-id>  # in a workspace: applies to both sides
  ctx0 disable <integration-id>
  ctx0 doctor                   # consistency + security checks;
                               # in a workspace also the wire-protocol lock
  ctx0 docs sync                # refresh docs/packages/ from installed packages
  ctx0 upgrade [--docs]         # bump security packages + re-sync docs + doctor

The security plane (RASP, request signing, ALE, secure storage — mobile;
JWT, ALE, signing, RBAC, RLS — API) ships as compiled packages and is not
scaffoldable — see docs/SECURITY.md in a generated repo.''';

Future<void> main(List<String> args) async {
  if (args.isEmpty || args.first == '--help' || args.first == '-h') {
    stdout.writeln(_usage);
    exit(args.isEmpty ? 2 : 0);
  }

  final workspace = findWorkspace();
  switch (args.first) {
    case 'create':
      exit(await _create(args.sublist(1)));
    case 'status':
      if (workspace != null) {
        var code = 0;
        for (final repo in await workspaceRepos(workspace)) {
          stdout.writeln('--- ${repo.root.path} (${repo.catalog.kind})');
          if (cmdStatus(repo) != 0) code = 1;
        }
        exit(code);
      }
      exit(cmdStatus(await openRepo()));
    case 'doctor':
      exit(workspace != null
          ? await workspaceDoctor(workspace)
          : cmdDoctor(await openRepo()));
    case 'enable' when args.length == 2:
      exit(workspace != null
          ? await workspaceToggle(workspace, args[1], enable: true)
          : await cmdEnable(await openRepo(), args[1]));
    case 'disable' when args.length == 2:
      exit(workspace != null
          ? await workspaceToggle(workspace, args[1], enable: false)
          : await cmdDisable(await openRepo(), args[1]));
    case 'docs' when args.length == 2 && args[1] == 'sync':
      exit(await cmdDocsSync(await openRepo()));
    case 'upgrade':
      final docsOnly = args.contains('--docs');
      if (workspace != null) {
        var code = 0;
        for (final repo in await workspaceRepos(workspace)) {
          stdout.writeln('--- ${repo.root.path} (${repo.catalog.kind})');
          if (await cmdUpgrade(repo, docsOnly: docsOnly) != 0) code = 1;
        }
        exit(code);
      }
      exit(await cmdUpgrade(await openRepo(), docsOnly: docsOnly));
    default:
      stderr.writeln('error: unrecognized command "${args.join(' ')}"\n');
      stderr.writeln(_usage);
      exit(2);
  }
}

Future<int> _create(List<String> args) async {
  final parser = ArgParser()
    ..addOption('org', defaultsTo: 'com.example')
    ..addOption('with', defaultsTo: '')
    ..addOption('out')
    ..addOption('template-dir')
    ..addFlag('local-packages', negatable: false);
  final ArgResults parsed;
  try {
    parsed = parser.parse(args);
  } on FormatException catch (e) {
    stderr.writeln('error: ${e.message}\n\n$_usage');
    return 2;
  }
  const kinds = {'app': 'mobile', 'api': 'api', 'workspace': 'workspace'};
  if (parsed.rest.length != 2 || !kinds.containsKey(parsed.rest.first)) {
    stderr.writeln(
        'error: expected `ctx0 create <app|api|workspace> <name>`.\n\n$_usage');
    return 2;
  }
  final name = parsed.rest[1];
  final org = parsed.option('org')!;
  final withIntegrations = parsed
      .option('with')!
      .split(',')
      .where((s) => s.trim().isNotEmpty)
      .map((s) => s.trim())
      .toList();
  final outDir = Directory(parsed.option('out') ?? name);
  final localPackages = parsed.flag('local-packages');

  switch (parsed.rest.first) {
    case 'workspace':
      return createWorkspace(
        name: name,
        org: org,
        withIntegrations: withIntegrations,
        outDir: outDir,
        localPackages: localPackages,
        templateDirFlag: parsed.option('template-dir'),
      );
    case 'api':
      final templateDir =
          await resolveTemplateDir('api', parsed.option('template-dir'));
      if (templateDir == null) {
        stderr.writeln('error: could not locate the api template. Pass '
            '--template-dir or set CTX_TEMPLATES.');
        return 2;
      }
      return createApi(
        name: name,
        org: org,
        withIntegrations: withIntegrations,
        templateDir: templateDir,
        outDir: outDir,
        localPackages: localPackages,
      );
    default:
      final templateDir =
          await resolveTemplateDir('mobile', parsed.option('template-dir'));
      if (templateDir == null) {
        stderr.writeln('error: could not locate the mobile template. Pass '
            '--template-dir or set CTX_TEMPLATES.');
        return 2;
      }
      return createApp(
        name: name,
        org: org,
        withIntegrations: withIntegrations,
        templateDir: templateDir,
        outDir: outDir,
        localPackages: localPackages,
      );
  }
}
