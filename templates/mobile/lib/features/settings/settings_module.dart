import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app/feature_module.dart';
import '../../core/config/app_config.dart';
import '../../core/l10n/l10n.dart';
import '../../core/l10n/locale_cubit.dart';
import '../../core/theme/theme_cubit.dart';
import '../../data/repositories/auth_repository.dart';
import 'bloc/settings_cubit.dart';
import 'views/settings_screen.dart';

/// Shipped settings module: hosts the composed settings screen and
/// contributes the shell's own sections — Personalisation (theme,
/// language) and Privacy/GDPR (docs/APP_SHELL.md §3–4).
class SettingsModule extends FeatureModule {
  const SettingsModule();

  @override
  List<RouteBase> get routes => [
        GoRoute(
          path: '/settings',
          builder: (context, state) => BlocProvider(
            create: (context) => SettingsCubit(
              authRepository: context.read<AuthRepository>(),
            ),
            child: const SettingsScreen(),
          ),
        ),
      ];

  @override
  NavItem? get navItem => NavItem(
        rootRoute: '/settings',
        icon: Icons.settings_outlined,
        selectedIcon: Icons.settings,
        label: (context) => context.l10n.settingsTitle,
      );

  @override
  List<SettingsSection> get settingsSections => [
        SettingsSection(
          title: (context) => context.l10n.settingsPersonalisationSection,
          tiles: (context) => [
            const _ThemeTile(),
            const _LanguageTile(),
          ],
        ),
        SettingsSection(
          title: (context) => context.l10n.settingsPrivacySection,
          tiles: (context) => [
            ListTile(
              leading: const Icon(Icons.download_outlined),
              title: Text(context.l10n.exportMyData),
              onTap: () => context.read<SettingsCubit>().requestDataExport(),
            ),
            ListTile(
              leading: const Icon(Icons.policy_outlined),
              title: Text(context.l10n.privacyPolicy),
              onTap: () => launchUrl(Uri.parse(AppConfig.privacyPolicyUrl)),
            ),
            ListTile(
              leading: const Icon(Icons.description_outlined),
              title: Text(context.l10n.termsOfService),
              onTap: () => launchUrl(Uri.parse(AppConfig.termsOfServiceUrl)),
            ),
            const _DeleteAccountTile(),
          ],
        ),
      ];
}

class _ThemeTile extends StatelessWidget {
  const _ThemeTile();

  String _label(BuildContext context, ThemeMode mode) => switch (mode) {
        ThemeMode.system => context.l10n.themeSystem,
        ThemeMode.light => context.l10n.themeLight,
        ThemeMode.dark => context.l10n.themeDark,
      };

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<ThemeCubit, ThemeMode>(
      builder: (context, mode) => ListTile(
        leading: const Icon(Icons.brightness_6_outlined),
        title: Text(context.l10n.themeLabel),
        trailing: DropdownButton<ThemeMode>(
          value: mode,
          underline: const SizedBox.shrink(),
          onChanged: (value) {
            if (value != null) context.read<ThemeCubit>().setMode(value);
          },
          items: [
            for (final m in ThemeMode.values)
              DropdownMenuItem(value: m, child: Text(_label(context, m))),
          ],
        ),
      ),
    );
  }
}

class _LanguageTile extends StatelessWidget {
  const _LanguageTile();

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<LocaleCubit, Locale?>(
      builder: (context, locale) => ListTile(
        leading: const Icon(Icons.language_outlined),
        title: Text(context.l10n.languageLabel),
        trailing: DropdownButton<Locale?>(
          value: locale,
          underline: const SizedBox.shrink(),
          onChanged: (value) => context.read<LocaleCubit>().setLocale(value),
          items: [
            DropdownMenuItem(
              value: null,
              child: Text(context.l10n.languageSystem),
            ),
            for (final supported in AppLocalizations.supportedLocales)
              DropdownMenuItem(
                value: supported,
                child: Text(supported.languageCode.toUpperCase()),
              ),
          ],
        ),
      ),
    );
  }
}

class _DeleteAccountTile extends StatelessWidget {
  const _DeleteAccountTile();

  Future<void> _confirmDelete(BuildContext context) async {
    final cubit = context.read<SettingsCubit>();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(dialogContext.l10n.deleteAccountConfirmTitle),
        content: Text(dialogContext.l10n.deleteAccountConfirmBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(dialogContext.l10n.cancel),
          ),
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(dialogContext.l10n.deleteConfirm),
          ),
        ],
      ),
    );
    if (confirmed ?? false) await cubit.deleteAccount();
  }

  @override
  Widget build(BuildContext context) {
    final error = Theme.of(context).colorScheme.error;
    return ListTile(
      leading: Icon(Icons.delete_forever_outlined, color: error),
      title: Text(
        context.l10n.deleteAccount,
        style: TextStyle(color: error),
      ),
      onTap: () => _confirmDelete(context),
    );
  }
}
