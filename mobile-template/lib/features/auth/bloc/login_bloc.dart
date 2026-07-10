import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../data/repositories/auth_repository.dart';
import '../data/google_auth_service.dart';

part 'login_event.dart';
part 'login_state.dart';

/// Login screen Bloc — a Bloc (not Cubit) because several triggers map to
/// one state machine (docs/STATE_MANAGEMENT.md §1). Submissions are
/// droppable: a double-tap can never double-submit (§4).
class LoginBloc extends Bloc<LoginEvent, LoginState> {
  LoginBloc({
    required AuthRepository authRepository,
    required GoogleAuthService googleAuth,
  })  : _authRepository = authRepository,
        _googleAuth = googleAuth,
        super(const LoginInitial()) {
    on<LoginSubmitted>(_onSubmitted, transformer: droppable());
    on<LoginWithGooglePressed>(_onGooglePressed, transformer: droppable());
  }

  final AuthRepository _authRepository;
  final GoogleAuthService _googleAuth;

  Future<void> _onSubmitted(
    LoginSubmitted event,
    Emitter<LoginState> emit,
  ) async {
    emit(const LoginLoading());
    final result = await _authRepository.login(event.email, event.password);
    switch (result) {
      case Success():
        emit(const LoginSuccess()); // Router redirect handles navigation.
      case Failure(:final error):
        emit(LoginFailure(AppException.from(error).userFriendlyMessage));
    }
  }

  Future<void> _onGooglePressed(
    LoginWithGooglePressed event,
    Emitter<LoginState> emit,
  ) async {
    emit(const LoginLoading());
    try {
      final idToken = await _googleAuth.signIn();
      if (idToken == null) {
        emit(const LoginInitial()); // User cancelled.
        return;
      }
      final result = await _authRepository.signInWithGoogle(idToken);
      switch (result) {
        case Success():
          emit(const LoginSuccess());
        case Failure(:final error):
          emit(LoginFailure(AppException.from(error).userFriendlyMessage));
      }
    } on Exception catch (e) {
      emit(LoginFailure(AppException.from(e).userFriendlyMessage));
    }
  }
}
