part of 'auth_bloc.dart';

sealed class AuthEvent {
  const AuthEvent();
}

/// Fired once at app start to bind the Bloc to the repository stream.
final class AuthSubscriptionRequested extends AuthEvent {
  const AuthSubscriptionRequested();
}

final class AuthLogoutRequested extends AuthEvent {
  const AuthLogoutRequested();
}
