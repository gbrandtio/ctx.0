import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../data/repositories/auth_repository.dart';

part 'signup_event.dart';
part 'signup_state.dart';

/// Signup screen Bloc; submission is droppable (docs/STATE_MANAGEMENT.md
/// §4). Consents collected here are sent with the signup request
/// (docs/features/SIGNUP.md; the set is configured in AppConfig).
class SignupBloc extends Bloc<SignupEvent, SignupState> {
  SignupBloc({required AuthRepository authRepository})
      : _authRepository = authRepository,
        super(const SignupInitial()) {
    on<SignupSubmitted>(_onSubmitted, transformer: droppable());
  }

  final AuthRepository _authRepository;

  Future<void> _onSubmitted(
    SignupSubmitted event,
    Emitter<SignupState> emit,
  ) async {
    emit(const SignupLoading());
    final result = await _authRepository.signup(
      email: event.email,
      password: event.password,
      displayName: event.displayName,
      consents: event.consents,
    );
    switch (result) {
      case Success():
        emit(const SignupSuccess());
      case Failure(:final error):
        emit(SignupFailure(AppException.from(error).userFriendlyMessage));
    }
  }
}
