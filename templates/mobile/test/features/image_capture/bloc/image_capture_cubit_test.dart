import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image_picker/image_picker.dart';
import 'package:mocktail/mocktail.dart';

import 'package:app_template/core/result/result.dart';
import 'package:app_template/features/image_capture/bloc/image_capture_cubit.dart';
import 'package:app_template/features/image_capture/bloc/image_capture_state.dart';
import 'package:app_template/features/image_capture/data/image_capture_service.dart';

class MockImageCaptureService extends Mock implements ImageCaptureService {}

void main() {
  setUpAll(() {
    registerFallbackValue(ImageSource.camera);
  });

  group('ImageCaptureCubit', () {
    late MockImageCaptureService mockImageCaptureService;

    setUp(() {
      mockImageCaptureService = MockImageCaptureService();
    });

    test('initial state is ImageCaptureInitial', () {
      expect(
        ImageCaptureCubit(imageCaptureService: mockImageCaptureService).state,
        equals(const ImageCaptureInitial()),
      );
    });

    blocTest<ImageCaptureCubit, ImageCaptureState>(
      'emits [ImageCaptureLoading, ImageCaptureSuccess] when captureImage succeeds with an image',
      build: () {
        when(() => mockImageCaptureService.pickImage(any())).thenAnswer(
            (_) async => Result.success(XFile('path/to/image')));
        return ImageCaptureCubit(imageCaptureService: mockImageCaptureService);
      },
      act: (cubit) => cubit.captureImage(ImageSource.camera),
      expect: () => [
        const ImageCaptureLoading(),
        isA<ImageCaptureSuccess>()
            .having((s) => s.image.path, 'image path', 'path/to/image'),
      ],
    );

    blocTest<ImageCaptureCubit, ImageCaptureState>(
      'emits [ImageCaptureLoading, ImageCaptureInitial] when captureImage succeeds with null (user cancelled)',
      build: () {
        when(() => mockImageCaptureService.pickImage(any()))
            .thenAnswer((_) async => const Result.success(null));
        return ImageCaptureCubit(imageCaptureService: mockImageCaptureService);
      },
      act: (cubit) => cubit.captureImage(ImageSource.camera),
      expect: () => [
        const ImageCaptureLoading(),
        const ImageCaptureInitial(),
      ],
    );

    blocTest<ImageCaptureCubit, ImageCaptureState>(
      'emits [ImageCaptureLoading, ImageCaptureFailure] when captureImage fails',
      build: () {
        when(() => mockImageCaptureService.pickImage(any()))
            .thenAnswer((_) async => const Result.failure('Error occurred'));
        return ImageCaptureCubit(imageCaptureService: mockImageCaptureService);
      },
      act: (cubit) => cubit.captureImage(ImageSource.camera),
      expect: () => [
        const ImageCaptureLoading(),
        isA<ImageCaptureFailure>()
            .having((s) => s.error, 'error', 'Error occurred'),
      ],
    );
  });
}
