import 'package:bloc/bloc.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../data/repositories/auth_repository.dart';

part 'verify_email_state.dart';

/// Email-verification screen state holder — a simple form, so a Cubit
/// (docs/STATE_MANAGEMENT.md §1).
class VerifyEmailCubit extends Cubit<VerifyEmailState> {
  VerifyEmailCubit({required AuthRepository authRepository})
      : _authRepository = authRepository,
        super(const VerifyEmailInitial());

  final AuthRepository _authRepository;

  Future<void> verify(String code) async {
    if (state is VerifyEmailSubmitting) return;
    emit(const VerifyEmailSubmitting());
    final result = await _authRepository.verifyEmail(code);
    switch (result) {
      case Success():
        emit(const VerifyEmailVerified());
      case Failure(:final error):
        emit(VerifyEmailFailure(AppException.from(error).userFriendlyMessage));
    }
  }

  Future<void> resend() async {
    if (state is VerifyEmailSubmitting) return;
    emit(const VerifyEmailSubmitting());
    final result = await _authRepository.resendVerification();
    switch (result) {
      case Success():
        emit(const VerifyEmailResent());
      case Failure(:final error):
        emit(VerifyEmailFailure(AppException.from(error).userFriendlyMessage));
    }
  }
}
