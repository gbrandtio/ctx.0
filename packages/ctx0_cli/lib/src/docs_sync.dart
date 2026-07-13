import 'dart:convert';
import 'dart:io';

import 'injector.dart';

/// Packages whose embedded consumer docs are materialized into the
/// generated repo's `docs/packages/` by `ctx0 docs sync`. The doc travels
/// INSIDE the versioned package artifact (its README), so what lands here
/// always describes the installed version — never edit these copies.
const mobileDocPackages = ['ctx0_mobile_security'];
const apiDocPackages = ['Ctx0.Security.Abstractions', 'Ctx0.Security', 'Ctx0.Security.EfCore'];

Future<int> cmdDocsSync(InjectorRepo repo) async {
  final sources = repo.catalog.kind == 'mobile'
      ? mobilePackageDirs(repo)
      : apiPackageDirs(repo);
  if (sources == null) return 1;

  final outDir = Directory('${repo.root.path}/docs/packages')
    ..createSync(recursive: true);
  var synced = 0;
  sources.forEach((name, packageDir) {
    final readme = File('${packageDir.path}/README.md');
    if (!readme.existsSync()) {
      stderr.writeln('warning: $name ships no README.md — skipped.');
      return;
    }
    final version = _versionOf(name, packageDir) ?? 'unknown';
    File('${outDir.path}/$name.md').writeAsStringSync(
        '<!-- ctx:generated $name $version — do not edit; run `ctx docs '
        'sync` after upgrading the package -->\n'
        '${readme.readAsStringSync()}');
    stdout.writeln('  synced docs/packages/$name.md ($version)');
    synced++;
  });
  stdout.writeln(synced == 0
      ? 'docs sync: nothing synced.'
      : 'docs sync: $synced package doc(s) up to date.');
  return 0;
}

/// `docs/packages/(pkg).md` version headers must match the installed
/// packages; consumed by `ctx0 doctor`.
List<String> docsDriftProblems(InjectorRepo repo) {
  final problems = <String>[];
  final outDir = Directory('${repo.root.path}/docs/packages');
  if (!outDir.existsSync()) return problems;
  final sources = repo.catalog.kind == 'mobile'
      ? mobilePackageDirs(repo, quiet: true)
      : apiPackageDirs(repo, quiet: true);
  if (sources == null) return problems;
  sources.forEach((name, packageDir) {
    final doc = File('${outDir.path}/$name.md');
    if (!doc.existsSync()) return;
    final header = RegExp('ctx:generated $name (\\S+)')
        .firstMatch(doc.readAsLinesSync().first);
    final installed = _versionOf(name, packageDir);
    if (header != null &&
        installed != null &&
        header.group(1) != installed) {
      problems.add('docs: docs/packages/$name.md describes '
          '${header.group(1)} but $installed is installed — run '
          '`ctx0 docs sync`');
    }
  });
  return problems;
}

Map<String, Directory>? mobilePackageDirs(InjectorRepo repo, {bool quiet = false}) {
  final configFile = File('${repo.root.path}/.dart_tool/package_config.json');
  if (!configFile.existsSync()) {
    if (!quiet) {
      stderr.writeln('error: .dart_tool/package_config.json missing — run '
          '`flutter pub get` first.');
    }
    return null;
  }
  final config =
      jsonDecode(configFile.readAsStringSync()) as Map<String, dynamic>;
  final packages = (config['packages'] as List).cast<Map<String, dynamic>>();
  final result = <String, Directory>{};
  for (final name in mobileDocPackages) {
    final entry = packages.where((p) => p['name'] == name).firstOrNull;
    if (entry == null) {
      if (!quiet) {
        stderr.writeln('warning: $name not in package_config.json — skipped.');
      }
      continue;
    }
    final rootUri = entry['rootUri'] as String;
    result[name] = rootUri.startsWith('file://')
        ? Directory.fromUri(Uri.parse(rootUri))
        : Directory.fromUri(configFile.parent.uri.resolve(rootUri));
  }
  return result;
}

/// API packages resolve through the NuGet global-packages folder for
/// hosted references, or straight to the project directory for
/// repo-local ProjectReferences.
Map<String, Directory>? apiPackageDirs(InjectorRepo repo, {bool quiet = false}) {
  final home = Platform.environment['HOME'] ??
      Platform.environment['USERPROFILE'] ??
      '';
  final result = <String, Directory>{};
  final csprojs = repo.root
      .listSync(recursive: true, followLinks: false)
      .whereType<File>()
      .where((f) => f.path.endsWith('.csproj'))
      .toList();
  for (final name in apiDocPackages) {
    Directory? found;
    for (final csproj in csprojs) {
      final text = csproj.readAsStringSync();
      final pkg = RegExp('<PackageReference Include="$name" '
              'Version="([^"]+)"')
          .firstMatch(text);
      if (pkg != null) {
        final dir = Directory(
            '$home/.nuget/packages/${name.toLowerCase()}/${pkg.group(1)}');
        if (dir.existsSync()) found = dir;
      }
      final proj = RegExp('<ProjectReference Include="([^"]*[\\\\/]'
              '$name[\\\\/][^"]*)"')
          .firstMatch(text);
      if (proj != null) {
        final refPath = proj.group(1)!.replaceAll(r'\', '/');
        final dir = Directory(refPath.startsWith('/')
            ? refPath.substring(0, refPath.lastIndexOf('/'))
            : '${csproj.parent.path}/${refPath.substring(0, refPath.lastIndexOf('/'))}');
        if (dir.existsSync()) found = dir;
      }
      if (found != null) break;
    }
    if (found == null) {
      if (!quiet) stderr.writeln('warning: $name not referenced — skipped.');
      continue;
    }
    result[name] = found;
  }
  return result;
}

String? _versionOf(String name, Directory packageDir) {
  final pubspec = File('${packageDir.path}/pubspec.yaml');
  if (pubspec.existsSync()) {
    return RegExp(r'^version:\s*(\S+)', multiLine: true)
        .firstMatch(pubspec.readAsStringSync())
        ?.group(1);
  }
  final csproj = File('${packageDir.path}/$name.csproj');
  if (csproj.existsSync()) {
    return RegExp(r'<Version>([^<]+)</Version>')
        .firstMatch(csproj.readAsStringSync())
        ?.group(1);
  }
  // NuGet cache layout: <root>/<id>/<version>/...
  final segments = packageDir.path.split('/');
  return segments.isEmpty ? null : segments.last;
}
