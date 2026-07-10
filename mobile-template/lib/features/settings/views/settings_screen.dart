import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../app/feature_module.dart';
import '../../../core/l10n/l10n.dart';
import '../../../core/widgets/app_header.dart';
import '../bloc/settings_cubit.dart';

/// The settings screen is an ordered composition of the SettingsSections
/// every module contributed (docs/APP_SHELL.md §3) — features never edit
/// this file.
class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final sections = context.read<ModuleRegistry>().settingsSections;
    return Scaffold(
      appBar: AppHeader(
        config: HeaderConfig(
          title: (context) => context.l10n.settingsTitle,
          showBackButton: false,
        ),
      ),
      body: BlocListener<SettingsCubit, SettingsState>(
        listener: (context, state) {
          switch (state) {
            case SettingsExportRequested():
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(
                  SnackBar(content: Text(context.l10n.exportRequested)),
                );
            case SettingsFailure(:final message):
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(SnackBar(content: Text(message)));
            default:
              break;
          }
        },
        child: ListView(
          padding: const EdgeInsets.symmetric(vertical: 16),
          children: [
            for (final section in sections) ...[
              Padding(
                padding: const EdgeInsets.fromLTRB(24, 16, 24, 8),
                child: Text(
                  section.title(context),
                  style: Theme.of(context).textTheme.titleSmall?.copyWith(
                        color: Theme.of(context).colorScheme.primary,
                      ),
                ),
              ),
              Card(child: Column(children: section.tiles(context))),
            ],
          ],
        ),
      ),
    );
  }
}
