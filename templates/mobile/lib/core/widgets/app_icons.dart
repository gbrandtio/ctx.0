import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

/// Brand SVG assets (docs/UI_UX_GUIDELINES.md §4D). SVG sources live in
/// docs/brand-kit/ and are bundled via assets/brand/; register each one
/// here — never load raw asset paths from feature code.
abstract final class AppIcons {
  static const String googleLogo = 'assets/brand/google_logo.svg';
}

/// Renders a brand SVG with consistent sizing and optional theme-aware
/// tinting. Partner logos (e.g. the Google "G") must NOT be recolored.
class AppIcon extends StatelessWidget {
  const AppIcon(this.asset, {super.key, this.size = 24, this.tinted = false});

  final String asset;
  final double size;

  /// Tint with the primary color; leave false for partner logos.
  final bool tinted;

  @override
  Widget build(BuildContext context) {
    return SvgPicture.asset(
      asset,
      width: size,
      height: size,
      colorFilter: tinted
          ? ColorFilter.mode(
              Theme.of(context).colorScheme.primary,
              BlendMode.srcIn,
            )
          : null,
    );
  }
}
