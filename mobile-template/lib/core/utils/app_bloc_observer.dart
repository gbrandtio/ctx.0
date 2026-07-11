import 'package:bloc/bloc.dart';
import 'package:flutter/foundation.dart';

/// Global observer (docs/STATE_MANAGEMENT.md §7): logs transitions and
/// errors in debug builds. In release builds, forward onError to your
/// crash reporter here. Never log state payloads containing PII.
class AppBlocObserver extends BlocObserver {
  const AppBlocObserver();

  @override
  void onTransition(
    Bloc<dynamic, dynamic> bloc,
    Transition<dynamic, dynamic> transition,
  ) {
    super.onTransition(bloc, transition);
    if (kDebugMode) {
      debugPrint('${bloc.runtimeType}: '
          '${transition.currentState.runtimeType} '
          '→ ${transition.nextState.runtimeType} '
          '(${transition.event.runtimeType})');
    }
  }

  @override
  void onError(BlocBase<dynamic> bloc, Object error, StackTrace stackTrace) {
    if (kDebugMode) {
      debugPrint('${bloc.runtimeType} error: $error\n$stackTrace');
    }
    // TODO(template): forward to your crash reporter in release builds.
    super.onError(bloc, error, stackTrace);
  }
}
