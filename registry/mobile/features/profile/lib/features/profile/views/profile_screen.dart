import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../core/l10n/l10n.dart';
import '../../../core/widgets/app_header.dart';
import '../bloc/profile_cubit.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Scaffold(
      appBar: AppHeader(
        config: HeaderConfig(
          title: (context) => context.l10n.profileTitle,
          showBackButton: false,
          actions: [
            Builder(
              builder: (context) => IconButton(
                icon: const Icon(Icons.edit_outlined),
                tooltip: context.l10n.editProfile,
                onPressed: () => context.go('/profile/edit'),
              ),
            ),
          ],
        ),
      ),
      body: BlocConsumer<ProfileCubit, ProfileState>(
        listenWhen: (previous, current) =>
            current.status == ProfileStatus.failure &&
            previous.status != ProfileStatus.failure,
        listener: (context, state) {
          final message = state.errorMessage;
          if (message != null) {
            ScaffoldMessenger.of(context)
              ..hideCurrentSnackBar()
              ..showSnackBar(SnackBar(content: Text(message)));
          }
        },
        builder: (context, state) {
          final user = state.user;
          return RefreshIndicator(
            onRefresh: () => context.read<ProfileCubit>().refresh(),
            child: ListView(
              padding: const EdgeInsets.symmetric(vertical: 16),
              children: [
                if (user == null)
                  const Center(child: CircularProgressIndicator())
                else ...[
                  Card(
                    child: ListTile(
                      leading: const Icon(Icons.person_outline),
                      title: Text(user.displayName ?? '—'),
                      subtitle: Text(l10n.displayNameLabel),
                    ),
                  ),
                  Card(
                    child: ListTile(
                      leading: const Icon(Icons.email_outlined),
                      title: Text(user.email),
                      subtitle: Text(l10n.emailLabel),
                      // The API verifies the email during registration, so
                      // an authenticated account is always verified.
                      trailing: const Icon(Icons.verified_outlined),
                    ),
                  ),
                ],
              ],
            ),
          );
        },
      ),
    );
  }
}
