/// Abstraction for time retrieval to allow for testable dates.
abstract class TimeProvider {
  DateTime get now;
}

/// Native implementation using the system clock.
class SystemTimeProvider implements TimeProvider {
  const SystemTimeProvider();

  @override
  DateTime get now => DateTime.now();
}
