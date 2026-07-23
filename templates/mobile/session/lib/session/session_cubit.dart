import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'token_store.dart';

/// Where the app-wide session stands: not yet resolved, signed out, or signed in.
enum SessionStatus { unknown, anonymous, authenticated }

/// Immutable session state. The auth gate and any UI that varies by sign-in
/// render from [status].
final class SessionState extends Equatable {
  const SessionState({this.status = SessionStatus.unknown});

  final SessionStatus status;

  @override
  List<Object?> get props => [status];
}

/// The single source of truth for whether someone is signed in.
///
/// It holds no credentials of its own: the tokens live in [ctxSession], which it
/// reads on [restore] to decide the initial status and listens to for a session
/// dropped on renewal. A provider that authenticates (the `auth` feature) stores
/// the tokens and then calls [signedIn]/[signedOut]; providers do not keep
/// session state of their own. When no such provider is enabled the session
/// simply stays [SessionStatus.anonymous].
class SessionCubit extends Cubit<SessionState> {
  SessionCubit(this._credentials) : super(const SessionState()) {
    // A refresh the API rejects ends the session without the user asking, so the
    // gate has to be told; otherwise the shell stays up over a dead session.
    _lost = _credentials.sessionLost.listen((_) {
      emit(const SessionState(status: SessionStatus.anonymous));
    });
  }

  final TokenStore _credentials;
  late final StreamSubscription<void> _lost;

  /// Resolve the initial status from whatever is in secure storage.
  Future<void> restore() async {
    final token = await _credentials.readAccessToken();
    emit(
      SessionState(
        status: token != null
            ? SessionStatus.authenticated
            : SessionStatus.anonymous,
      ),
    );
  }

  /// A provider has installed credentials; the session is now authenticated.
  void signedIn() =>
      emit(const SessionState(status: SessionStatus.authenticated));

  /// A provider has cleared credentials; the session is now anonymous.
  void signedOut() => emit(const SessionState(status: SessionStatus.anonymous));

  @override
  Future<void> close() {
    _lost.cancel();
    return super.close();
  }
}
