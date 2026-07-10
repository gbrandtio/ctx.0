import 'dart:async';

import 'package:bloc/bloc.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../models/user.dart';

part 'profile_state.dart';

/// Profile screen state holder. Keeps data visible during refreshes, so a
/// single state class with a status enum (docs/STATE_MANAGEMENT.md §3).
/// The user itself comes from the AuthRepository stream — the SSOT.
class ProfileCubit extends Cubit<ProfileState> {
  ProfileCubit({required AuthRepository authRepository})
      : _authRepository = authRepository,
        super(ProfileState.from(authRepository.currentState)) {
    _authSubscription = authRepository.authStateChanges.listen((authState) {
      if (authState is Authenticated) {
        emit(state.copyWith(user: authState.user));
      }
    });
  }

  final AuthRepository _authRepository;
  late final StreamSubscription<AuthState> _authSubscription;

  /// Pull-to-refresh: bypasses the HTTP cache
  /// (docs/CACHING_IMPLEMENTATION.md "Manual Force Refresh").
  Future<void> refresh() async {
    if (state.status == ProfileStatus.refreshing) return;
    emit(state.copyWith(status: ProfileStatus.refreshing));
    final result = await _authRepository.refreshUser(forceRefresh: true);
    switch (result) {
      case Success():
        // The stream listener above delivers the merged user.
        emit(state.copyWith(status: ProfileStatus.idle));
      case Failure(:final error):
        emit(state.copyWith(
          status: ProfileStatus.failure,
          errorMessage: AppException.from(error).userFriendlyMessage,
        ));
    }
  }

  Future<void> save({required String displayName}) async {
    if (state.status == ProfileStatus.saving) return;
    emit(state.copyWith(status: ProfileStatus.saving));
    final result =
        await _authRepository.updateProfile(displayName: displayName);
    switch (result) {
      case Success():
        emit(state.copyWith(status: ProfileStatus.saveSuccess));
      case Failure(:final error):
        emit(state.copyWith(
          status: ProfileStatus.failure,
          errorMessage: AppException.from(error).userFriendlyMessage,
        ));
    }
  }

  @override
  Future<void> close() async {
    await _authSubscription.cancel();
    return super.close();
  }
}
