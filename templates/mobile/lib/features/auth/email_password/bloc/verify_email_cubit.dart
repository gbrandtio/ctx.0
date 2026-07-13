import 'package:bloc/bloc.dart';
import 'package:equatable/equatable.dart';

import '../../../../core/result/result.dart';
import '../../../../core/utils/app_exception.dart';
import '../../../../data/repositories/auth_repository.dart';
import '../data/pending_registration.dart';

part 'verify_email_state.dart';

/// Signup step 2 (AUTHENTICATION.md): submit the emailed code together
/// with the pending registration to create the account and start the
/// session. A simple form, so a Cubit (docs/STATE_MANAGEMENT.md §1).
class VerifyEmailCubit extends Cubit<VerifyEmailState> {
  VerifyEmailCubit({
    required AuthRepository authRepository,
    required PendingRegistration pending,
  }) : _authRepository = authRepository,
       _pending = pending,
       super(const VerifyEmailInitial());

  final AuthRepository _authRepository;
  final PendingRegistration _pending;

  Future<void> verify(String code) async {
    if (state is VerifyEmailSubmitting) return;
    emit(const VerifyEmailSubmitting());
    final result = await _authRepository.register(
      username: _pending.username,
      email: _pending.email,
      password: _pending.password,
      verificationCode: code,
      displayName: _pending.displayName,
      consents: _pending.consents,
    );
    switch (result) {
      case Success():
        // The router redirect navigates home once authenticated.
        emit(const VerifyEmailVerified());
      case Failure(:final error):
        emit(VerifyEmailFailure(AppException.from(error).userFriendlyMessage));
    }
  }

  Future<void> resend() async {
    if (state is VerifyEmailSubmitting) return;
    emit(const VerifyEmailSubmitting());
    final result = await _authRepository.sendSignupCode(_pending.email);
    switch (result) {
      case Success():
        emit(const VerifyEmailResent());
      case Failure(:final error):
        emit(VerifyEmailFailure(AppException.from(error).userFriendlyMessage));
    }
  }
}
