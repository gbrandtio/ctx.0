/// The `ctx:<id>:begin`/`ctx:<id>:end` marker engine shared by the CLI and
/// the per-template fallback `tool/scaffold.dart`. Enabling/disabling an
/// integration comments or uncomments the lines inside its marker blocks
/// in place — the feature's code always stays in the tree — so the two
/// engines produce byte-identical results and never disagree on state.
library;

const offToken = 'ctx:off';

/// Line-comment token per file type; XML-ish files (`.xml`, `.plist`,
/// `.csproj`) have no line comment and use `<!-- ctx:off ... ctx:off -->`
/// wrapper lines instead, signalled by a null token.
String? commentTokenFor(String path) {
  if (path.endsWith('.yaml') || path.endsWith('.yml')) return '#';
  if (path.endsWith('.dart') ||
      path.endsWith('.kts') ||
      path.endsWith('.swift') ||
      path.endsWith('.cs')) {
    return '//';
  }
  if (path.endsWith('.xml') ||
      path.endsWith('.plist') ||
      path.endsWith('.csproj')) {
    return null;
  }
  throw StateError('No comment style known for $path');
}

class Block {
  Block(this.start, this.end); // line indexes of the begin/end marker lines
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
          .where(
            (l) => l.trim() != '<!-- $offToken' && l.trim() != '$offToken -->',
          )
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
