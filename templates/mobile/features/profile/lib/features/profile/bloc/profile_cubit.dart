import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../data/profile_repository.dart';

enum ProfileStatus { idle, loading, ready, saving, failure }

/// Immutable state for the profile screen; the view rebuilds purely from it.
final class ProfileState extends Equatable {
  const ProfileState({
    this.status = ProfileStatus.idle,
    this.profile,
    this.error,
  });

  final ProfileStatus status;
  final ProfileData? profile;
  final String? error;

  ProfileState copyWith({
    ProfileStatus? status,
    ProfileData? profile,
    String? error,
  }) =>
      ProfileState(
        status: status ?? this.status,
        profile: profile ?? this.profile,
        error: error,
      );

  @override
  List<Object?> get props => [status, profile, error];
}

/// Drives the profile screen. All I/O lives here; the view only renders.
class ProfileCubit extends Cubit<ProfileState> {
  ProfileCubit(this._repository) : super(const ProfileState());

  final ProfileRepository _repository;

  Future<void> load() async {
    emit(state.copyWith(status: ProfileStatus.loading, error: null));
    try {
      final profile = await _repository.get();
      emit(state.copyWith(status: ProfileStatus.ready, profile: profile));
    } catch (e) {
      emit(state.copyWith(status: ProfileStatus.failure, error: e.toString()));
    }
  }

  Future<void> save({required String displayName, String? bio, String? avatarUrl}) async {
    emit(state.copyWith(status: ProfileStatus.saving, error: null));
    try {
      final profile = await _repository.update(displayName: displayName, bio: bio, avatarUrl: avatarUrl);
      emit(state.copyWith(status: ProfileStatus.ready, profile: profile));
    } catch (e) {
      emit(state.copyWith(status: ProfileStatus.failure, error: e.toString()));
    }
  }
}
