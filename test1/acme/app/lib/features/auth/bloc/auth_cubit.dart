import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../data/auth_repository.dart';

enum AuthStatus { unknown, unauthenticated, authenticating, authenticated, failure }

/// Immutable authentication state; the auth gate and login view render from it.
final class AuthState extends Equatable {
  const AuthState({this.status = AuthStatus.unknown, this.error});

  final AuthStatus status;
  final String? error;

  @override
  List<Object?> get props => [status, error];
}

/// Owns the session lifecycle: restoring a stored session, logging in/registering,
/// and logging out. All I/O goes through [AuthRepository].
class AuthCubit extends Cubit<AuthState> {
  AuthCubit(this._repository) : super(const AuthState());

  final AuthRepository _repository;

  Future<void> restore() async {
    final authed = await _repository.hasSession();
    emit(AuthState(status: authed ? AuthStatus.authenticated : AuthStatus.unauthenticated));
  }

  Future<void> login(String email, String password) =>
      _run(() => _repository.login(email, password));

  Future<void> register(String email, String password) =>
      _run(() => _repository.register(email, password));

  Future<void> logout() async {
    await _repository.logout();
    emit(const AuthState(status: AuthStatus.unauthenticated));
  }

  Future<void> _run(Future<void> Function() action) async {
    emit(const AuthState(status: AuthStatus.authenticating));
    try {
      await action();
      emit(const AuthState(status: AuthStatus.authenticated));
    } catch (e) {
      emit(AuthState(status: AuthStatus.failure, error: e.toString()));
    }
  }
}
