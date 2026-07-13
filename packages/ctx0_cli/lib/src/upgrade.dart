import 'dart:io';

import 'commands.dart';
import 'docs_sync.dart';
import 'markers.dart';

/// `ctx0 upgrade [--docs]` — bump the security packages within their
/// compatible range (pub/NuGet constraints already pin the protocol
/// major.minor), re-materialize the package docs, and report drift.
/// `--docs` skips the dependency bump and only refreshes the docs.
Future<int> cmdUpgrade(MarkerRepo repo, {required bool docsOnly}) async {
  if (!docsOnly) {
    if (repo.catalog.kind == 'mobile') {
      stdout.writeln('Upgrading ${mobileDocPackages.join(', ')} ...');
      final result = await Process.run(
        'flutter',
        ['pub', 'upgrade', ...mobileDocPackages],
        workingDirectory: repo.root.path,
        runInShell: true,
      );
      stdout.write(result.stdout);
      if (result.exitCode != 0) {
        stderr.write(result.stderr);
        stderr.writeln('error: pub upgrade failed.');
        return 1;
      }
    } else {
      // NuGet: floating within the compatible range happens at restore
      // time only for wildcard versions; pinned versions need an explicit
      // bump. Restore, then report what is installed.
      stdout.writeln('Restoring packages ...');
      final result = await Process.run(
        'dotnet',
        ['restore'],
        workingDirectory: repo.root.path,
        runInShell: true,
      );
      if (result.exitCode != 0) {
        stderr.write(result.stderr);
        stderr.writeln('error: dotnet restore failed.');
        return 1;
      }
      stdout.writeln('note: pinned Ctx0.Security versions are bumped by '
          'editing the PackageReference Version (keep major.minor aligned '
          'with the mobile side — `ctx0 doctor` verifies the lock).');
    }
  }

  final docsResult = await cmdDocsSync(repo);
  if (docsResult != 0) return docsResult;

  return cmdDoctor(repo);
}
