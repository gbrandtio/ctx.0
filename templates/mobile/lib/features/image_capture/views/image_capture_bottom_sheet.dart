import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/l10n/app_localizations.dart';
import '../bloc/image_capture_cubit.dart';
import '../bloc/image_capture_state.dart';
import '../data/image_capture_service.dart';

class ImageCaptureBottomSheet extends StatelessWidget {
  const ImageCaptureBottomSheet({super.key});

  /// Displays the image capture bottom sheet.
  /// Returns the selected [XFile] if successful, or null if the user cancelled.
  static Future<XFile?> show(BuildContext context) {
    return showModalBottomSheet<XFile?>(
      context: context,
      builder: (context) => BlocProvider(
        create: (context) => ImageCaptureCubit(
          imageCaptureService: ImageCaptureService(),
        ),
        child: const ImageCaptureBottomSheet(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);

    return BlocConsumer<ImageCaptureCubit, ImageCaptureState>(
      listener: (context, state) {
        if (state is ImageCaptureSuccess) {
          Navigator.of(context).pop(state.image);
        } else if (state is ImageCaptureFailure) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text(l10n.imageCaptureError)),
          );
          Navigator.of(context).pop();
        }
      },
      builder: (context, state) {
        if (state is ImageCaptureLoading) {
          return const SafeArea(
            child: SizedBox(
              height: 150,
              child: Center(child: CircularProgressIndicator()),
            ),
          );
        }

        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ListTile(
                leading: const Icon(Icons.camera_alt),
                title: Text(l10n.imageCaptureCamera),
                onTap: () => context
                    .read<ImageCaptureCubit>()
                    .captureImage(ImageSource.camera),
              ),
              ListTile(
                leading: const Icon(Icons.photo_library),
                title: Text(l10n.imageCaptureGallery),
                onTap: () => context
                    .read<ImageCaptureCubit>()
                    .captureImage(ImageSource.gallery),
              ),
            ],
          ),
        );
      },
    );
  }
}
