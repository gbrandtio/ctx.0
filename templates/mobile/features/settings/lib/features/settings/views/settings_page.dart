import 'package:flutter/material.dart';

import 'package:ctxapp/l10n/gen/app_l10n.dart';

// ctx:gen:settings-imports

/// The settings hub, opened from the gear in the profile page's app bar. It is a
/// plain list of the opt-in controls other features contribute — one row per
/// enabled feature that declares a `settingsEntry` (the `l10n` language picker,
/// the `gdpr` privacy controls). The rows below are generated from the workspace's
/// enabled features; edit the choice with the ctx.0 tooling, or this file directly.
class SettingsPage extends StatelessWidget {
  const SettingsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final l = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l.settingsTitle)),
      body: ListView(
        children: <Widget>[
          // ctx:gen:settings-entries
        ],
      ),
    );
  }
}
