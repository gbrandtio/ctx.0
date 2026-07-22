import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../data/auth_repository.dart';

enum AuthStatus { idle, submitting, success, failure }

/// Immutable state of the login/registration form; the login view renders from it.
final class AuthState extends Equatable {
  const AuthState({this.status = AuthStatus.idle, this.error});

  final AuthStatus status;
  final String? error;

  @override
  List<Object?> get props => [status, error];
}

/// Drives the login/registration form. It performs the credential exchange
/// through [AuthRepository] and reports the outcome; it does *not* own the app's
/// sign-in status. On [AuthStatus.success] the view hands off to the session by
/// calling `SessionCubit.signedIn()`.
class AuthCubit extends Cubit<AuthState> {
  AuthCubit(this._repository) : super(const AuthState());

  final AuthRepository _repository;

  Future<void> login(String email, String password) =>
      _run(() => _repository.login(email, password));

  Future<void> register(String email, String password) =>
      _run(() => _repository.register(email, password));

  Future<void> _run(Future<void> Function() action) async {
    emit(const AuthState(status: AuthStatus.submitting));
    try {
      await action();
      emit(const AuthState(status: AuthStatus.success));
    } catch (e) {
      emit(AuthState(status: AuthStatus.failure, error: e.toString()));
    }
  }
}
