import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'package:ctxapp/l10n/gen/app_l10n.dart';

import '../bloc/consent_cubit.dart';

/// Overlays the app with the privacy notice until the user has answered it for
/// the current notice version. It sits above every route (wired into the base
/// app's `app-overlay` anchor) so the decision can be made before sign-in, and
/// the answer is recorded on the device immediately.
class ConsentBanner extends StatelessWidget {
  const ConsentBanner({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<ConsentCubit, ConsentState>(
      builder: (context, state) {
        if (!state.prompting) return child;
        return Stack(
          children: [
            child,
            const Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: _ConsentSheet(),
            ),
          ],
        );
      },
    );
  }
}

class _ConsentSheet extends StatelessWidget {
  const _ConsentSheet();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l = AppL10n.of(context);
    return SafeArea(
      child: Card(
        margin: const EdgeInsets.all(12),
        elevation: 8,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                l.gdprConsentBannerTitle,
                style: theme.textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              Text(l.gdprConsentBannerBody, style: theme.textTheme.bodyMedium),
              const SizedBox(height: 16),
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton(
                    onPressed: () =>
                        context.read<ConsentCubit>().essentialOnly(),
                    child: Text(l.gdprEssentialOnly),
                  ),
                  const SizedBox(width: 8),
                  FilledButton(
                    onPressed: () => context.read<ConsentCubit>().acceptAll(),
                    child: Text(l.gdprAcceptAll),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
