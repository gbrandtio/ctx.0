import 'dart:io';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/theme/app_colors.dart';

/// A global overlay that blocks the UI and forces the user to update the app
/// if the backend returns a 426 Upgrade Required.
class AppUpdatesOverlay extends StatelessWidget {
  const AppUpdatesOverlay({
    super.key,
    required this.notifier,
    required this.child,
  });

  /// The global notifier triggered by the VersionCheckClient.
  final ValueNotifier<bool> notifier;

  /// The main application router.
  final Widget child;

  Future<void> _launchStore() async {
    // TODO: Replace YOUR_APP_ID with the actual App Store numeric ID
    final url = Platform.isIOS
        ? 'https://apps.apple.com/app/idYOUR_APP_ID'
        : 'https://play.google.com/store/apps/details?id=com.example.app_template';

    final uri = Uri.parse(url);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.ltr,
      child: Stack(
        children: [
          child,
          ValueListenableBuilder<bool>(
            valueListenable: notifier,
            builder: (context, updateRequired, _) {
              if (!updateRequired) return const SizedBox.shrink();

              // Use an absorbing pointer to block all touches to the underlying app
              return AbsorbPointer(
                child: Container(
                  color: AppColors.backgroundDark.withValues(alpha: 0.95),
                  padding: const EdgeInsets.all(32),
                  child: Center(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(
                          Icons.system_update,
                          size: 64,
                          color: AppColors.primary,
                        ),
                        const SizedBox(height: 24),
                        Text(
                          'Update Required',
                          style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                            color: AppColors.textPrimaryDark,
                          ),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 16),
                        Text(
                          'A new version of the app is required to continue. Please update to the latest version.',
                          style: Theme.of(context).textTheme.bodyLarge?.copyWith(
                            color: AppColors.textSecondaryDark,
                          ),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 32),
                        SizedBox(
                          width: double.infinity,
                          height: 48,
                          child: ElevatedButton(
                            style: ElevatedButton.styleFrom(
                              backgroundColor: AppColors.primary,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(24),
                              ),
                            ),
                            onPressed: _launchStore,
                            child: Text(
                              'Update Now',
                              style: Theme.of(context).textTheme.labelLarge?.copyWith(
                                color: Colors.white,
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}
