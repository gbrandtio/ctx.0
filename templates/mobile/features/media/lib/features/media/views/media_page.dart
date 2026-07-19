import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:image_picker/image_picker.dart';

import '../bloc/media_cubit.dart';

/// Lists the signed-in user's stored files with sizes; a FAB picks an image and
/// uploads it, and each row can be deleted.
class MediaPage extends StatefulWidget {
  const MediaPage({super.key});

  @override
  State<MediaPage> createState() => _MediaPageState();
}

class _MediaPageState extends State<MediaPage> {
  final ImagePicker _picker = ImagePicker();

  @override
  void initState() {
    super.initState();
    context.read<MediaCubit>().load();
  }

  Future<void> _pickAndUpload() async {
    final picked = await _picker.pickImage(source: ImageSource.gallery);
    if (picked == null || !mounted) return;
    final bytes = await picked.readAsBytes();
    await context.read<MediaCubit>().upload(
          fileName: picked.name,
          contentType: picked.mimeType ?? 'application/octet-stream',
          bytes: bytes,
        );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Media')),
      floatingActionButton: BlocBuilder<MediaCubit, MediaState>(
        builder: (context, state) => FloatingActionButton(
          onPressed: state.uploading ? null : _pickAndUpload,
          child: state.uploading
              ? const SizedBox(width: 24, height: 24, child: CircularProgressIndicator(strokeWidth: 2))
              : const Icon(Icons.upload_file),
        ),
      ),
      body: BlocBuilder<MediaCubit, MediaState>(
        builder: (context, state) {
          if (state.status == MediaStatus.loading && state.items.isEmpty) {
            return const Center(child: CircularProgressIndicator());
          }
          if (state.status == MediaStatus.failure && state.items.isEmpty) {
            return Center(child: Text('Error: ${state.error}', style: const TextStyle(color: Colors.red)));
          }
          if (state.items.isEmpty) {
            return const Center(child: Text('No files yet — tap + to upload'));
          }
          return RefreshIndicator(
            onRefresh: () => context.read<MediaCubit>().load(),
            child: ListView.builder(
              itemCount: state.items.length,
              itemBuilder: (context, i) {
                final item = state.items[i];
                return ListTile(
                  leading: Icon(item.isImage ? Icons.image : Icons.insert_drive_file),
                  title: Text(item.fileName),
                  subtitle: Text(_formatSize(item.sizeBytes)),
                  trailing: IconButton(
                    icon: const Icon(Icons.delete_outline),
                    onPressed: () => context.read<MediaCubit>().delete(item.id),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }

  static String _formatSize(int bytes) {
    if (bytes < 1024) return '$bytes B';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).toStringAsFixed(1)} KB';
    return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
  }
}
