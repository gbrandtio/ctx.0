/// Result pattern per docs/FLUTTER_ARCHITECTURE.md §3B.
///
/// Repositories catch low-level exceptions and return a [Result]; Blocs
/// `switch` on it exhaustively to emit success/failure states. Exceptions
/// never cross the Data → UI boundary.
sealed class Result<T> {
  const Result();

  const factory Result.success(T value) = Success<T>;
  const factory Result.failure(Object error) = Failure<T>;
}

final class Success<T> extends Result<T> {
  const Success(this.value);
  final T value;
}

final class Failure<T> extends Result<T> {
  const Failure(this.error);
  final Object error;
}
