import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'package:ctxapp/l10n/gen/app_l10n.dart';
import 'package:ctxapp/session/session_cubit.dart';
import 'package:ctxapp/features/auth/bloc/auth_cubit.dart';
import 'package:ctxapp/features/settings/views/settings_page.dart';

import '../bloc/profile_cubit.dart';
import '../data/profile_repository.dart';

/// Shows and edits the signed-in user's profile (display name, bio, avatar URL).
class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key});

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  final _displayName = TextEditingController();
  final _bio = TextEditingController();
  final _avatarUrl = TextEditingController();
  bool _hydrated = false;

  @override
  void initState() {
    super.initState();
    context.read<ProfileCubit>().load();
  }

  @override
  void dispose() {
    _displayName.dispose();
    _bio.dispose();
    _avatarUrl.dispose();
    super.dispose();
  }

  void _hydrate(ProfileData profile) {
    if (_hydrated) return;
    _displayName.text = profile.displayName;
    _bio.text = profile.bio ?? '';
    _avatarUrl.text = profile.avatarUrl ?? '';
    _hydrated = true;
  }

  void _save() {
    context.read<ProfileCubit>().save(
          displayName: _displayName.text.trim(),
          bio: _bio.text.trim().isEmpty ? null : _bio.text.trim(),
          avatarUrl: _avatarUrl.text.trim().isEmpty ? null : _avatarUrl.text.trim(),
        );
  }

  void _openSettings() {
    Navigator.of(context).push(MaterialPageRoute(builder: (_) => const SettingsPage()));
  }

  /// Ends the session and hands control back to the auth gate. The session is
  /// read before the await so nothing touches this widget's context across the
  /// gap; auth revokes server-side, then the session is told it is signed out.
  Future<void> _logout() async {
    final session = context.read<SessionCubit>();
    await context.read<AuthCubit>().logout();
    session.signedOut();
  }

  @override
  Widget build(BuildContext context) {
    final l = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Text(l.profileTitle),
        actions: [
          IconButton(
            icon: const Icon(Icons.settings),
            tooltip: l.settingsTitle,
            onPressed: _openSettings,
          ),
        ],
      ),
      body: BlocConsumer<ProfileCubit, ProfileState>(
        listener: (context, state) {
          if (state.profile != null) _hydrate(state.profile!);
          if (state.status == ProfileStatus.failure && state.error != null) {
            ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(state.error!)));
          }
        },
        builder: (context, state) {
          if (state.status == ProfileStatus.loading && state.profile == null) {
            return const Center(child: CircularProgressIndicator());
          }
          final avatarUrl = _avatarUrl.text.trim();
          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Center(
                child: CircleAvatar(
                  radius: 44,
                  backgroundImage: avatarUrl.isEmpty ? null : NetworkImage(avatarUrl),
                  child: avatarUrl.isEmpty ? const Icon(Icons.person, size: 44) : null,
                ),
              ),
              const SizedBox(height: 24),
              TextField(
                controller: _displayName,
                decoration: InputDecoration(labelText: l.profileDisplayNameLabel),
                textInputAction: TextInputAction.next,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _bio,
                decoration: InputDecoration(labelText: l.profileBioLabel),
                maxLines: 3,
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _avatarUrl,
                decoration: InputDecoration(labelText: l.profileAvatarUrlLabel),
                keyboardType: TextInputType.url,
                onChanged: (_) => setState(() {}),
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: state.status == ProfileStatus.saving ? null : _save,
                child: state.status == ProfileStatus.saving
                    ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(strokeWidth: 2))
                    : Text(l.commonSave),
              ),
              const SizedBox(height: 8),
              TextButton.icon(
                onPressed: _logout,
                icon: const Icon(Icons.logout),
                label: Text(l.profileLogout),
              ),
            ],
          );
        },
      ),
    );
  }
}
