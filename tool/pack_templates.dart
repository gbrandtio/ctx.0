// Publish-time template packer: copies `templates/mobile` and
// `templates/api` into `packages/ctx0_cli/templates/` so `ctx0 create`
// works offline from the installed CLI package. Run before
// `dart pub publish` in packages/ctx0_cli:
//
//   dart run tool/pack_templates.dart
//
// The embedded copy is .gitignore'd (the sources of truth stay under
// templates/); packages/ctx0_cli/.pubignore makes pub include it anyway.
// Repo-local dependency paths are left untouched: `ctx0 create` rewrites
// them to hosted references at generation time.

import 'dart:io';

const skipDirs = {
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

void main() {
  final repoRoot = File.fromUri(Platform.script).parent.parent;
  final target = Directory('${repoRoot.path}/packages/ctx0_cli/templates');
  if (target.existsSync()) {
    target.deleteSync(recursive: true);
  }
  for (final kind in ['mobile', 'api']) {
    final from = Directory('${repoRoot.path}/templates/$kind');
    if (!from.existsSync()) {
      stderr.writeln('error: ${from.path} missing');
      exit(1);
    }
    _copyTree(from, Directory('${target.path}/$kind'));
    stdout.writeln('packed templates/$kind');
  }
  stdout.writeln('done — publish packages/ctx0_cli to ship them.');
}

void _copyTree(Directory from, Directory to) {
  to.createSync(recursive: true);
  for (final entity in from.listSync(followLinks: false)) {
    final basename = entity.uri.pathSegments.lastWhere((s) => s.isNotEmpty);
    if (entity is Directory) {
      if (skipDirs.contains(basename)) continue;
      _copyTree(entity, Directory('${to.path}/$basename'));
    } else if (entity is File) {
      if (basename == '.DS_Store' || basename == 'pubspec.lock') continue;
      entity.copySync('${to.path}/$basename');
    }
  }
}
