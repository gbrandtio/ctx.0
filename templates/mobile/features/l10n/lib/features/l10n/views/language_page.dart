import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'package:ctxapp/l10n/gen/app_l10n.dart';
import 'package:ctxapp/l10n/l10n_support.dart';

import '../bloc/locale_cubit.dart';

/// Lets the user pick the app's language, or hand the choice back to the device.
///
/// The list is [AppL10nSupport.supportedLocales] — the languages this workspace
/// was generated with — and each is named in its own language, so it is legible
/// to someone who cannot yet read the current one.
class LanguagePage extends StatelessWidget {
  const LanguagePage({super.key});

  @override
  Widget build(BuildContext context) {
    final l = AppL10n.of(context);
    final selected = context.watch<LocaleCubit>().state;

    return Scaffold(
      appBar: AppBar(title: Text(l.languageTitle)),
      body: ListView(
        children: [
          ListTile(
            title: Text(l.languageUseDevice),
            subtitle: Text(l.languageUseDeviceHint),
            trailing: selected == null ? const Icon(Icons.check) : null,
            selected: selected == null,
            onTap: () => context.read<LocaleCubit>().useDeviceLanguage(),
          ),
          const Divider(height: 1),
          for (final locale in AppL10nSupport.supportedLocales)
            ListTile(
              title: Text(AppL10nSupport.languageName(locale)),
              trailing: locale == selected ? const Icon(Icons.check) : null,
              selected: locale == selected,
              onTap: () => context.read<LocaleCubit>().select(locale),
            ),
        ],
      ),
    );
  }
}
