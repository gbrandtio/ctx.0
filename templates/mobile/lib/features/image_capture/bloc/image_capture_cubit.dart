import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/result/result.dart';
import '../data/image_capture_service.dart';
import 'image_capture_state.dart';

class ImageCaptureCubit extends Cubit<ImageCaptureState> {
  ImageCaptureCubit({required ImageCaptureService imageCaptureService})
    : _imageCaptureService = imageCaptureService,
      super(const ImageCaptureInitial());

  final ImageCaptureService _imageCaptureService;

  Future<void> captureImage(ImageSource source) async {
    emit(const ImageCaptureLoading());

    final result = await _imageCaptureService.pickImage(source);

    switch (result) {
      case Success(:final value):
        if (value != null) {
          emit(ImageCaptureSuccess(value));
        } else {
          // User cancelled the picker, revert to initial state.
          emit(const ImageCaptureInitial());
        }
      case Failure(:final error):
        emit(ImageCaptureFailure(error));
    }
  }
}
