import 'dart:async';

import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/session/session_cubit.dart';
import 'package:ctxapp/session/token_store.dart';

/// In-memory credentials, standing in for the platform secure storage. Only the
/// access token and the `sessionLost` signal matter to [SessionCubit].
class FakeTokenStore implements TokenStore {
  FakeTokenStore({this.access});

  String? access;
  final StreamController<void> lost = StreamController<void>.broadcast();

  @override
  Future<String?> readAccessToken() async => access;

  @override
  Future<String?> readRefreshToken() async => null;

  @override
  Future<DateTime?> readAccessExpiry() async => null;

  @override
  Future<void> save({
    required String accessToken,
    required String refreshToken,
    required DateTime accessExpiresAt,
  }) async => access = accessToken;

  @override
  Future<void> clear() async => access = null;

  @override
  Stream<void> get sessionLost => lost.stream;
}

void main() {
  test('starts unknown until the stored session is resolved', () {
    expect(SessionCubit(FakeTokenStore()).state.status, SessionStatus.unknown);
  });

  blocTest<SessionCubit, SessionState>(
    'restore with a stored token resolves to authenticated',
    build: () => SessionCubit(FakeTokenStore(access: 'jwt')),
    act: (cubit) => cubit.restore(),
    expect: () => [const SessionState(status: SessionStatus.authenticated)],
  );

  blocTest<SessionCubit, SessionState>(
    'restore with no token resolves to anonymous',
    build: () => SessionCubit(FakeTokenStore()),
    act: (cubit) => cubit.restore(),
    expect: () => [const SessionState(status: SessionStatus.anonymous)],
  );

  blocTest<SessionCubit, SessionState>(
    'a provider signing in marks the session authenticated',
    build: () => SessionCubit(FakeTokenStore()),
    act: (cubit) => cubit.signedIn(),
    expect: () => [const SessionState(status: SessionStatus.authenticated)],
  );

  blocTest<SessionCubit, SessionState>(
    'a provider signing out marks the session anonymous',
    build: () => SessionCubit(FakeTokenStore(access: 'jwt')),
    act: (cubit) => cubit.signedOut(),
    expect: () => [const SessionState(status: SessionStatus.anonymous)],
  );

  final expired = FakeTokenStore(access: 'jwt');
  blocTest<SessionCubit, SessionState>(
    'a session lost on renewal returns to anonymous',
    build: () => SessionCubit(expired),
    act: (_) => expired.lost.add(null),
    wait: const Duration(milliseconds: 1),
    expect: () => [const SessionState(status: SessionStatus.anonymous)],
  );
}
