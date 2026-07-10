import 'package:flutter/material.dart';

/// Declarative app-bar configuration (docs/APP_SHELL.md §2). Screens
/// declare a [HeaderConfig]; [AppHeader] renders it from theme tokens.
/// Hand-built per-screen AppBars are forbidden — extend this config
/// instead so styling stays centralized.
class HeaderConfig {
  const HeaderConfig({
    this.title,
    this.actions = const [],
    this.showBackButton = true,
    this.transparent = false,
    this.centerTitle,
  });

  /// Localized title, resolved at build time.
  final String Function(BuildContext context)? title;

  final List<Widget> actions;
  final bool showBackButton;

  /// Transparent over content (e.g. a map or hero image).
  final bool transparent;

  final bool? centerTitle;
}

/// The shell's single app-bar widget. All styling comes from
/// `AppBarTheme` in AppTheme (docs/UI_UX_GUIDELINES.md).
class AppHeader extends StatelessWidget implements PreferredSizeWidget {
  const AppHeader({super.key, this.config = const HeaderConfig()});

  final HeaderConfig config;

  @override
  Size get preferredSize => const Size.fromHeight(kToolbarHeight);

  @override
  Widget build(BuildContext context) {
    final title = config.title?.call(context);
    return AppBar(
      title: title == null ? null : Text(title),
      actions: config.actions.isEmpty ? null : config.actions,
      automaticallyImplyLeading: config.showBackButton,
      centerTitle: config.centerTitle,
      backgroundColor: config.transparent ? Colors.transparent : null,
      elevation: config.transparent ? 0 : null,
    );
  }
}
