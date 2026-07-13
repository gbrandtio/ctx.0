import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../../core/result/result.dart';
import '../../../../core/utils/app_exception.dart';
import '../../../../core/utils/time_provider.dart';
import '../../../../data/repositories/auth_repository.dart';
import '../data/pending_registration.dart';

part 'signup_event.dart';
part 'signup_state.dart';

/// Signup step 1 (AUTHENTICATION.md): validate the form and request an
/// email verification code. The account is created on the verify screen
/// with that code. Submission is droppable (docs/STATE_MANAGEMENT.md §4).
class SignupBloc extends Bloc<SignupEvent, SignupState> {
  SignupBloc({
    required AuthRepository authRepository,
    required TimeProvider timeProvider,
  }) : _authRepository = authRepository,
       _timeProvider = timeProvider,
       super(const SignupInitial()) {
    on<SignupSubmitted>(_onSubmitted, transformer: droppable());
  }

  final AuthRepository _authRepository;
  final TimeProvider _timeProvider;

  Future<void> _onSubmitted(
    SignupSubmitted event,
    Emitter<SignupState> emit,
  ) async {
    emit(const SignupLoading());
    final pending = PendingRegistration.fromForm(
      email: event.email,
      password: event.password,
      displayName: event.displayName,
      consents: event.consents,
      timeProvider: _timeProvider,
    );
    final result = await _authRepository.sendSignupCode(event.email);
    switch (result) {
      case Success():
        emit(SignupCodeSent(pending));
      case Failure(:final error):
        emit(SignupFailure(AppException.from(error).userFriendlyMessage));
    }
  }
}
