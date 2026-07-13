import 'package:ctx0_cli/src/markers.dart';
import 'package:test/test.dart';

void main() {
  group('findBlocks', () {
    test('finds multiple blocks and rejects nesting/unclosed', () {
      final lines = [
        '# ctx:maps:begin',
        'a: 1',
        '# ctx:maps:end',
        'other',
        '# ctx:maps:begin',
        'b: 2',
        '# ctx:maps:end',
      ];
      final blocks = findBlocks(lines, 'maps');
      expect(blocks, hasLength(2));
      expect(
          () => findBlocks(
              ['# ctx:x:begin', '# ctx:x:begin', '# ctx:x:end'], 'x'),
          throwsStateError);
      expect(() => findBlocks(['# ctx:x:begin'], 'x'), throwsStateError);
      expect(() => findBlocks(['# ctx:x:end'], 'x'), throwsStateError);
    });
  });

  group('line-comment blocks (yaml/dart)', () {
    test('disable/enable round-trips and reports state', () {
      var lines = [
        '# ctx:maps:begin',
        '  google_maps_flutter: ^2.0.0',
        '# ctx:maps:end',
      ];
      final block = findBlocks(lines, 'maps').single;
      expect(blockState(lines, block, '#'), BlockState.enabled);

      lines = transformBlock(lines, block, '#', enable: false);
      expect(lines[1], '  # ctx:off google_maps_flutter: ^2.0.0');
      expect(blockState(lines, findBlocks(lines, 'maps').single, '#'),
          BlockState.disabled);

      lines = transformBlock(
          lines, findBlocks(lines, 'maps').single, '#',
          enable: true);
      expect(lines[1], '  google_maps_flutter: ^2.0.0');
      expect(blockState(lines, findBlocks(lines, 'maps').single, '#'),
          BlockState.enabled);
    });

    test('disable is idempotent and detects mixed state', () {
      var lines = [
        '// ctx:maps:begin',
        'import "a.dart";',
        '// ctx:off import "b.dart";',
        '// ctx:maps:end',
      ];
      final block = findBlocks(lines, 'maps').single;
      expect(blockState(lines, block, '//'), BlockState.mixed);

      lines = transformBlock(lines, block, '//', enable: false);
      expect(lines[1], '// ctx:off import "a.dart";');
      expect(lines[2], '// ctx:off import "b.dart";'); // untouched, not doubled
    });
  });

  group('XML wrapper blocks', () {
    test('disable wraps, enable unwraps', () {
      var lines = [
        '    <!-- ctx:maps:begin -->',
        '    <meta-data android:name="maps" />',
        '    <!-- ctx:maps:end -->',
      ];
      final block = findBlocks(lines, 'maps').single;
      expect(blockState(lines, block, null), BlockState.enabled);

      lines = transformBlock(lines, block, null, enable: false);
      expect(lines[1].trim(), '<!-- ctx:off');
      expect(lines[3].trim(), 'ctx:off -->');
      expect(blockState(lines, findBlocks(lines, 'maps').single, null),
          BlockState.disabled);

      lines = transformBlock(
          lines, findBlocks(lines, 'maps').single, null,
          enable: true);
      expect(lines[1], '    <meta-data android:name="maps" />');
    });
  });

  group('commentTokenFor', () {
    test('maps known extensions and rejects unknown', () {
      expect(commentTokenFor('pubspec.yaml'), '#');
      expect(commentTokenFor('a.dart'), '//');
      expect(commentTokenFor('b.kts'), '//');
      expect(commentTokenFor('c.swift'), '//');
      expect(commentTokenFor('d.cs'), '//');
      expect(commentTokenFor('e.xml'), isNull);
      expect(commentTokenFor('f.plist'), isNull);
      expect(commentTokenFor('g.csproj'), isNull);
      expect(() => commentTokenFor('h.png'), throwsStateError);
    });
  });
}
