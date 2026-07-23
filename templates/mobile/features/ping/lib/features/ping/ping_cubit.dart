import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'ping_repository.dart';

enum PingStatus { idle, sending, success, failure }

/// Immutable state for the ping screen; the view rebuilds purely from this.
final class PingState extends Equatable {
  const PingState({this.status = PingStatus.idle, this.echo, this.error});

  final PingStatus status;
  final String? echo;
  final String? error;

  PingState copyWith({PingStatus? status, String? echo, String? error}) =>
      PingState(status: status ?? this.status, echo: echo, error: error);

  @override
  List<Object?> get props => [status, echo, error];
}

/// Drives the secure ping round trip. All I/O lives here; the view only renders.
class PingCubit extends Cubit<PingState> {
  PingCubit(this._repository) : super(const PingState());

  final PingRepository _repository;

  Future<void> send(String message) async {
    emit(state.copyWith(status: PingStatus.sending, echo: null, error: null));
    try {
      final echo = await _repository.ping(message);
      emit(state.copyWith(status: PingStatus.success, echo: echo));
    } catch (e) {
      emit(state.copyWith(status: PingStatus.failure, error: e.toString()));
    }
  }
}
