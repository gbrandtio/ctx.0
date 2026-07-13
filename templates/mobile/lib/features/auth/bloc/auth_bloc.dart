import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';

import '../../../data/repositories/auth_repository.dart';

part 'auth_event.dart';

/// Global auth Bloc (docs/STATE_MANAGEMENT.md §2): mirrors the
/// AuthRepository SSOT stream so widgets can read the session, and owns
/// the logout intent. Its state IS the repository's [AuthState].
class AuthBloc extends Bloc<AuthEvent, AuthState> {
  AuthBloc({required AuthRepository authRepository})
    : _authRepository = authRepository,
      super(authRepository.currentState) {
    on<AuthSubscriptionRequested>(_onSubscriptionRequested);
    on<AuthLogoutRequested>(_onLogoutRequested, transformer: droppable());
  }

  final AuthRepository _authRepository;

  Future<void> _onSubscriptionRequested(
    AuthSubscriptionRequested event,
    Emitter<AuthState> emit,
  ) {
    return emit.forEach(
      _authRepository.authStateChanges,
      onData: (state) => state,
    );
  }

  Future<void> _onLogoutRequested(
    AuthLogoutRequested event,
    Emitter<AuthState> emit,
  ) async {
    // State transitions arrive via the repository stream.
    await _authRepository.logout();
  }
}
