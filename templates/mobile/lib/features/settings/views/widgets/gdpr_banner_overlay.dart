import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../../core/l10n/l10n.dart';
import '../../../../data/repositories/auth_repository.dart';
import '../../../../data/services/storage/prefs_service.dart';

/// Wraps the shell navigator to display a GDPR banner and sticky warning.
class GdprBannerOverlay extends StatefulWidget {
  const GdprBannerOverlay({required this.child, super.key});

  final Widget child;

  @override
  State<GdprBannerOverlay> createState() => _GdprBannerOverlayState();
}

class _GdprBannerOverlayState extends State<GdprBannerOverlay> {
  bool _showBanner = false;

  @override
  void initState() {
    super.initState();
    final prefs = context.read<PrefsService>();
    if (!prefs.hasSeenGdprBanner) {
      _showBanner = true;
    }
  }

  void _accept() {
    final prefs = context.read<PrefsService>();
    final auth = context.read<AuthRepository>();
    prefs.setHasSeenGdprBanner(true);
    prefs.setTrackingConsentGranted(true);
    auth.updateProfile(hasTrackingConsent: true);
    setState(() {
      _showBanner = false;
    });
  }

  void _decline() {
    final prefs = context.read<PrefsService>();
    final auth = context.read<AuthRepository>();
    prefs.setHasSeenGdprBanner(true);
    prefs.setTrackingConsentGranted(false);
    auth.updateProfile(hasTrackingConsent: false);
    setState(() {
      _showBanner = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final prefs = context.read<PrefsService>();

    return StreamBuilder<bool>(
      stream: prefs.trackingConsentChanges,
      initialData: prefs.trackingConsentGranted,
      builder: (context, snapshot) {
        final trackingGranted = snapshot.data ?? false;
        final showStickyWarning = prefs.hasSeenGdprBanner && !trackingGranted;

        return Column(
          children: [
            // The sticky warning if consent is declined
            if (!_showBanner && showStickyWarning)
              Material(
                color: Theme.of(context).colorScheme.errorContainer,
                child: SafeArea(
                  bottom: false,
                  child: Container(
                    width: double.infinity,
                    padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                    child: Row(
                      children: [
                        Icon(
                          Icons.warning_amber_rounded,
                          color: Theme.of(context).colorScheme.onErrorContainer,
                          size: 20,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            context.l10n.gdprWarningMessage,
                            style: TextStyle(
                              color: Theme.of(context).colorScheme.onErrorContainer,
                              height: 1.2,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),

            // The main application content
            Expanded(
              child: Stack(
                children: [
                  widget.child,

                  // The consent banner
                  if (_showBanner)
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: 0,
                      child: Material(
                        elevation: 8,
                        color: Theme.of(context).colorScheme.surfaceContainerHighest,
                        borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
                        child: SafeArea(
                          top: false,
                          child: Padding(
                            padding: const EdgeInsets.all(24),
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Row(
                                  children: [
                                    Icon(
                                      Icons.privacy_tip_outlined,
                                      color: Theme.of(context).colorScheme.primary,
                                    ),
                                    const SizedBox(width: 12),
                                    Expanded(
                                      child: Text(
                                        context.l10n.gdprBannerTitle,
                                        style: Theme.of(context).textTheme.titleMedium,
                                      ),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 12),
                                Text(
                                  context.l10n.gdprBannerMessage,
                                  style: Theme.of(context).textTheme.bodyMedium,
                                ),
                                const SizedBox(height: 24),
                                Row(
                                  mainAxisAlignment: MainAxisAlignment.end,
                                  children: [
                                    TextButton(
                                      onPressed: _decline,
                                      child: Text(context.l10n.decline),
                                    ),
                                    const SizedBox(width: 8),
                                    FilledButton(
                                      onPressed: _accept,
                                      child: Text(context.l10n.accept),
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}
