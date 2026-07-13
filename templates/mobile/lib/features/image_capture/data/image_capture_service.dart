import 'package:image_picker/image_picker.dart';

import '../../../core/result/result.dart';

/// Service to handle device camera and gallery image picking.
class ImageCaptureService {
  ImageCaptureService({ImagePicker? picker}) : _picker = picker ?? ImagePicker();

  final ImagePicker _picker;

  /// Picks an image from the specified [source].
  /// Returns a [Result] containing the [XFile] if successful (or null if the user cancelled),
  /// or a [Failure] if an error occurred.
  Future<Result<XFile?>> pickImage(ImageSource source) async {
    try {
      final image = await _picker.pickImage(source: source);
      return Result.success(image);
    } catch (e) {
      return Result.failure(e);
    }
  }
}
