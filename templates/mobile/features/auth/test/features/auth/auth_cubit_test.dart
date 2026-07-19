import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/features/auth/bloc/auth_cubit.dart';
import 'package:ctxapp/features/auth/data/auth_repository.dart';

/// Configurable fake so the cubit can be tested without HTTP or storage.
class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.session = false, this.fail = false});

  bool session;
  final bool fail;

  @override
  Future<void> login(String email, String password) async {
    if (fail) throw const AuthException('bad credentials');
    session = true;
  }

  @override
  Future<void> register(String email, String password) async => login(email, password);

  @override
  Future<void> logout() async => session = false;

  @override
  Future<bool> hasSession() async => session;
}

void main() {
  blocTest<AuthCubit, AuthState>(
    'login success emits authenticating then authenticated',
    build: () => AuthCubit(FakeAuthRepository()),
    act: (cubit) => cubit.login('a@b.com', 'password1'),
    expect: () => [
      const AuthState(status: AuthStatus.authenticating),
      const AuthState(status: AuthStatus.authenticated),
    ],
  );

  blocTest<AuthCubit, AuthState>(
    'login failure emits authenticating then failure with a message',
    build: () => AuthCubit(FakeAuthRepository(fail: true)),
    act: (cubit) => cubit.login('a@b.com', 'wrong'),
    expect: () => [
      const AuthState(status: AuthStatus.authenticating),
      const AuthState(status: AuthStatus.failure, error: 'bad credentials'),
    ],
  );

  blocTest<AuthCubit, AuthState>(
    'restore reflects an existing session',
    build: () => AuthCubit(FakeAuthRepository(session: true)),
    act: (cubit) => cubit.restore(),
    expect: () => [const AuthState(status: AuthStatus.authenticated)],
  );

  blocTest<AuthCubit, AuthState>(
    'logout returns to unauthenticated',
    build: () => AuthCubit(FakeAuthRepository(session: true)),
    act: (cubit) => cubit.logout(),
    expect: () => [const AuthState(status: AuthStatus.unauthenticated)],
  );
}
