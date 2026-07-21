import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:share_plus/share_plus.dart';

import 'package:ctxapp/l10n/gen/app_l10n.dart';

import '../bloc/consent_cubit.dart';
import '../bloc/privacy_cubit.dart';

/// The user's privacy controls: review or withdraw consent, take a copy of the
/// account's data, and delete the account.
class PrivacyPage extends StatelessWidget {
  const PrivacyPage({super.key});

  @override
  Widget build(BuildContext context) {
    final l = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l.gdprTitle)),
      body: BlocConsumer<PrivacyCubit, PrivacyState>(
        listener: (context, state) {
          if (state.status == PrivacyStatus.failure && state.error != null) {
            ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(state.error!)));
          }
          if (state.status == PrivacyStatus.deleted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(l.gdprDeleted)),
            );
          }
        },
        builder: (context, state) {
          final busy = state.status == PrivacyStatus.exporting || state.status == PrivacyStatus.deleting;
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              const _ConsentSection(),
              const Divider(height: 32),
              Text(l.gdprYourDataTitle, style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(l.gdprYourDataBody),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: busy ? null : () => context.read<PrivacyCubit>().downloadMyData(),
                icon: const Icon(Icons.download),
                label: Text(
                  state.status == PrivacyStatus.exporting ? l.gdprPreparing : l.gdprDownloadMyData,
                ),
              ),
              if (state.archivePath != null) ...[
                const SizedBox(height: 12),
                Text(l.gdprSavedTo(state.archivePath!), style: Theme.of(context).textTheme.bodySmall),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: () => Share.shareXFiles([XFile(state.archivePath!)]),
                  icon: const Icon(Icons.ios_share),
                  label: Text(l.gdprShareArchive),
                ),
              ],
              const Divider(height: 32),
              Text(l.gdprDeleteSectionTitle, style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(l.gdprDeleteSectionBody),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: busy ? null : () => _confirmDelete(context),
                icon: const Icon(Icons.delete_forever),
                style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                label: Text(l.gdprDeleteMyAccount),
              ),
            ],
          );
        },
      ),
    );
  }

  Future<void> _confirmDelete(BuildContext context) async {
    final cubit = context.read<PrivacyCubit>();
    final password = await showDialog<String>(
      context: context,
      builder: (_) => const _DeleteAccountDialog(),
    );
    if (password != null && password.isNotEmpty) {
      await cubit.deleteAccount(password);
    }
  }
}

/// Shows what the account consented to, and lets the user change it.
class _ConsentSection extends StatelessWidget {
  const _ConsentSection();

  @override
  Widget build(BuildContext context) {
    final l = AppL10n.of(context);
    return BlocBuilder<ConsentCubit, ConsentState>(
      builder: (context, state) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(l.gdprConsentSectionTitle, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(
              state.decision == null
                  ? l.gdprNoDecision
                  : l.gdprRecordedAt(
                      state.decision!.decidedAt.toLocal(),
                      state.decision!.policyVersion,
                    ),
              style: Theme.of(context).textTheme.bodySmall,
            ),
            for (final purpose in ctxOptionalPurposes)
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(_purposeLabel(context, purpose)),
                value: state.accepted(purpose),
                onChanged: (enabled) {
                  final purposes = {
                    for (final p in ctxOptionalPurposes)
                      if (p == purpose ? enabled : state.accepted(p)) p,
                  };
                  context.read<ConsentCubit>().decide(purposes);
                },
              ),
          ],
        );
      },
    );
  }
}

/// The user-facing name of an optional processing purpose. The purposes are
/// identifiers on the wire (`analytics`, `marketing`); an id added to
/// [ctxOptionalPurposes] without a matching string falls back to the id itself,
/// so a new purpose shows up in the UI rather than disappearing from it.
String _purposeLabel(BuildContext context, String purpose) {
  final l = AppL10n.of(context);
  switch (purpose) {
    case 'analytics':
      return l.gdprPurposeAnalytics;
    case 'marketing':
      return l.gdprPurposeMarketing;
    default:
      return purpose;
  }
}

/// Re-authenticates before erasure: deleting an account is irreversible, so it
/// asks for the password and a typed confirmation.
class _DeleteAccountDialog extends StatefulWidget {
  const _DeleteAccountDialog();

  @override
  State<_DeleteAccountDialog> createState() => _DeleteAccountDialogState();
}

class _DeleteAccountDialogState extends State<_DeleteAccountDialog> {
  final _password = TextEditingController();
  final _confirm = TextEditingController();

  @override
  void dispose() {
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = AppL10n.of(context);
    final confirmed = _confirm.text.trim() == 'DELETE';
    return AlertDialog(
      title: Text(l.gdprDeleteDialogTitle),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(l.gdprDeleteDialogBody),
          const SizedBox(height: 16),
          TextField(
            controller: _confirm,
            decoration: InputDecoration(labelText: l.gdprTypeDeleteLabel),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 8),
          TextField(
            controller: _password,
            decoration: InputDecoration(labelText: l.authPasswordLabel),
            obscureText: true,
            onChanged: (_) => setState(() {}),
          ),
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: Text(l.commonCancel)),
        FilledButton(
          onPressed: confirmed && _password.text.isNotEmpty
              ? () => Navigator.of(context).pop(_password.text)
              : null,
          child: Text(l.commonDelete),
        ),
      ],
    );
  }
}
