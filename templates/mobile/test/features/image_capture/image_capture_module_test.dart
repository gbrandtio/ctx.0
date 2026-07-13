import 'package:flutter_test/flutter_test.dart';
import 'package:app_template/features/image_capture/image_capture_module.dart';

void main() {
  group('ImageCaptureModule', () {
    test('instantiates correctly', () {
      const module = ImageCaptureModule();
      expect(module.routes, isEmpty);
    });
  });
}
