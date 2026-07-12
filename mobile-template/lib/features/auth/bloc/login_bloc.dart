import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../data/repositories/auth_repository.dart';
// ctx:auth_google:begin
import '../google/google_auth_service.dart';
// ctx:auth_google:end

part 'login_event.dart';
part 'login_state.dart';

/// Login screen Bloc — a Bloc (not Cubit) because several triggers map to
/// one state machine (docs/STATE_MANAGEMENT.md §1). Submissions are
/// droppable: a double-tap can never double-submit (§4). The handlers for
/// each sign-in method sit inside that method's `ctx:` marker block
/// (docs/INTEGRATIONS.md).
class LoginBloc extends Bloc<LoginEvent, LoginState> {
  LoginBloc({
    required AuthRepository authRepository,
    // ctx:auth_google:begin
    required GoogleAuthService googleAuth,
    // ctx:auth_google:end
  })  : _authRepository = authRepository,
        // ctx:auth_google:begin
        _googleAuth = googleAuth,
        // ctx:auth_google:end
        super(const LoginInitial()) {
    // ctx:auth_email_password:begin
    on<LoginSubmitted>(_onSubmitted, transformer: droppable());
    // ctx:auth_email_password:end
    // ctx:auth_google:begin
    on<LoginWithGooglePressed>(_onGooglePressed, transformer: droppable());
    // ctx:auth_google:end
  }

  final AuthRepository _authRepository;
  // ctx:auth_google:begin
  final GoogleAuthService _googleAuth;
  // ctx:auth_google:end

  // ctx:auth_email_password:begin
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
  // ctx:auth_email_password:end

  // ctx:auth_google:begin
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
  // ctx:auth_google:end
}
