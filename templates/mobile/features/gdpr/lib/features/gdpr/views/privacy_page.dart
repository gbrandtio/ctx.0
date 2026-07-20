import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:share_plus/share_plus.dart';

import '../bloc/consent_cubit.dart';
import '../bloc/privacy_cubit.dart';

/// The user's privacy controls: review or withdraw consent, take a copy of the
/// account's data, and delete the account.
class PrivacyPage extends StatelessWidget {
  const PrivacyPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Privacy')),
      body: BlocConsumer<PrivacyCubit, PrivacyState>(
        listener: (context, state) {
          if (state.status == PrivacyStatus.failure && state.error != null) {
            ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(state.error!)));
          }
          if (state.status == PrivacyStatus.deleted) {
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(content: Text('Your account and data have been deleted.')),
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
              Text('Your data', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              const Text(
                'Get a copy of everything this account holds. The archive is built '
                'on the server and can be downloaded once.',
              ),
              const SizedBox(height: 12),
              FilledButton.icon(
                onPressed: busy ? null : () => context.read<PrivacyCubit>().downloadMyData(),
                icon: const Icon(Icons.download),
                label: Text(state.status == PrivacyStatus.exporting ? 'Preparing…' : 'Download my data'),
              ),
              if (state.archivePath != null) ...[
                const SizedBox(height: 12),
                Text('Saved to ${state.archivePath}', style: Theme.of(context).textTheme.bodySmall),
                const SizedBox(height: 8),
                OutlinedButton.icon(
                  onPressed: () => Share.shareXFiles([XFile(state.archivePath!)]),
                  icon: const Icon(Icons.ios_share),
                  label: const Text('Share archive'),
                ),
              ],
              const Divider(height: 32),
              Text('Delete account', style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              const Text(
                'This erases your account and everything stored with it, immediately '
                'and permanently. Download your data first if you want a copy.',
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: busy ? null : () => _confirmDelete(context),
                icon: const Icon(Icons.delete_forever),
                style: OutlinedButton.styleFrom(foregroundColor: Theme.of(context).colorScheme.error),
                label: const Text('Delete my account'),
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
    return BlocBuilder<ConsentCubit, ConsentState>(
      builder: (context, state) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Consent', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 4),
            Text(
              state.decision == null
                  ? 'No decision recorded yet.'
                  : 'Recorded ${state.decision!.decidedAt.toLocal()} against notice ${state.decision!.policyVersion}.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
            for (final purpose in ctxOptionalPurposes)
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(purpose[0].toUpperCase() + purpose.substring(1)),
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
    final confirmed = _confirm.text.trim() == 'DELETE';
    return AlertDialog(
      title: const Text('Delete your account?'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('This cannot be undone. Type DELETE and enter your password to confirm.'),
          const SizedBox(height: 16),
          TextField(
            controller: _confirm,
            decoration: const InputDecoration(labelText: 'Type DELETE'),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 8),
          TextField(
            controller: _password,
            decoration: const InputDecoration(labelText: 'Password'),
            obscureText: true,
            onChanged: (_) => setState(() {}),
          ),
        ],
      ),
      actions: [
        TextButton(onPressed: () => Navigator.of(context).pop(), child: const Text('Cancel')),
        FilledButton(
          onPressed: confirmed && _password.text.isNotEmpty
              ? () => Navigator.of(context).pop(_password.text)
              : null,
          child: const Text('Delete'),
        ),
      ],
    );
  }
}
