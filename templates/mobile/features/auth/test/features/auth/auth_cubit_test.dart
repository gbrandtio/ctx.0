import 'dart:async';

import 'package:bloc_test/bloc_test.dart';

import 'package:ctxapp/features/auth/bloc/auth_cubit.dart';
import 'package:ctxapp/features/auth/data/auth_repository.dart';

/// Configurable fake so the form cubit can be tested without HTTP or storage.
class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.session = false, this.fail = false});

  bool session;
  final bool fail;
  final StreamController<void> lost = StreamController<void>.broadcast();

  @override
  Stream<void> get sessionLost => lost.stream;

  @override
  Future<void> login(String email, String password) async {
    if (fail) throw const AuthException('bad credentials');
    session = true;
  }

  @override
  Future<void> register(String email, String password) async =>
      login(email, password);

  @override
  Future<void> logout() async => session = false;

  @override
  Future<bool> hasSession() async => session;
}

void main() {
  blocTest<AuthCubit, AuthState>(
    'login success emits submitting then success',
    build: () => AuthCubit(FakeAuthRepository()),
    act: (cubit) => cubit.login('a@b.com', 'password1'),
    expect: () => [
      const AuthState(status: AuthStatus.submitting),
      const AuthState(status: AuthStatus.success),
    ],
  );

  blocTest<AuthCubit, AuthState>(
    'register success emits submitting then success',
    build: () => AuthCubit(FakeAuthRepository()),
    act: (cubit) => cubit.register('a@b.com', 'password1'),
    expect: () => [
      const AuthState(status: AuthStatus.submitting),
      const AuthState(status: AuthStatus.success),
    ],
  );

  blocTest<AuthCubit, AuthState>(
    'login failure emits submitting then failure with a message',
    build: () => AuthCubit(FakeAuthRepository(fail: true)),
    act: (cubit) => cubit.login('a@b.com', 'wrong'),
    expect: () => [
      const AuthState(status: AuthStatus.submitting),
      const AuthState(status: AuthStatus.failure, error: 'bad credentials'),
    ],
  );
}
