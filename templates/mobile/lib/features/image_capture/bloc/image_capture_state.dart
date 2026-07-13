import 'package:equatable/equatable.dart';
import 'package:image_picker/image_picker.dart';

sealed class ImageCaptureState extends Equatable {
  const ImageCaptureState();

  @override
  List<Object?> get props => [];
}

final class ImageCaptureInitial extends ImageCaptureState {
  const ImageCaptureInitial();
}

final class ImageCaptureLoading extends ImageCaptureState {
  const ImageCaptureLoading();
}

final class ImageCaptureSuccess extends ImageCaptureState {
  const ImageCaptureSuccess(this.image);

  final XFile image;

  @override
  List<Object?> get props => [image.path];
}

final class ImageCaptureFailure extends ImageCaptureState {
  const ImageCaptureFailure(this.error);

  final Object error;

  @override
  List<Object?> get props => [error];
}
