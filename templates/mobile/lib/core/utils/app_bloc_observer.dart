import 'package:bloc/bloc.dart';

import 'logging_service.dart';

/// Global observer (docs/STATE_MANAGEMENT.md §7): logs transitions and
/// errors in debug builds. In release builds, forward onError to your
/// crash reporter via the [LoggingService]. Never log state payloads containing PII.
class AppBlocObserver extends BlocObserver {
  const AppBlocObserver(this._loggingService);

  final LoggingService _loggingService;

  @override
  void onTransition(
    Bloc<dynamic, dynamic> bloc,
    Transition<dynamic, dynamic> transition,
  ) {
    super.onTransition(bloc, transition);
    _loggingService.info('${bloc.runtimeType}: '
        '${transition.currentState.runtimeType} '
        '→ ${transition.nextState.runtimeType} '
        '(${transition.event.runtimeType})');
  }

  @override
  void onError(BlocBase<dynamic> bloc, Object error, StackTrace stackTrace) {
    _loggingService.error('${bloc.runtimeType} error', error, stackTrace);
    super.onError(bloc, error, stackTrace);
  }
}
