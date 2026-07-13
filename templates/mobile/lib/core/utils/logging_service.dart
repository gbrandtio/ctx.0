import 'dart:developer' as developer;
import 'package:flutter/foundation.dart';

/// Centralized logging service that handles debug printing
/// and routes errors to crashlytics in release mode.
abstract class LoggingService {
  void info(String message);
  void error(String message, [Object? error, StackTrace? stackTrace]);
}

class ConsoleLoggingService implements LoggingService {
  const ConsoleLoggingService();

  @override
  void info(String message) {
    if (kDebugMode) {
      debugPrint(message);
    }
  }

  @override
  void error(String message, [Object? error, StackTrace? stackTrace]) {
    if (kDebugMode) {
      developer.log(
        message,
        error: error,
        stackTrace: stackTrace,
        level: 1000, // Level.SEVERE equivalent
      );
    } else {
      // TODO(template): Forward to Crashlytics/Sentry in release builds
    }
  }
}
