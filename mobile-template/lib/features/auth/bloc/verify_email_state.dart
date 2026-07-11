part of 'verify_email_cubit.dart';

sealed class VerifyEmailState extends Equatable {
  const VerifyEmailState();

  @override
  List<Object?> get props => [];
}

final class VerifyEmailInitial extends VerifyEmailState {
  const VerifyEmailInitial();
}

final class VerifyEmailSubmitting extends VerifyEmailState {
  const VerifyEmailSubmitting();
}

final class VerifyEmailResent extends VerifyEmailState {
  const VerifyEmailResent();
}

final class VerifyEmailVerified extends VerifyEmailState {
  const VerifyEmailVerified();
}

final class VerifyEmailFailure extends VerifyEmailState {
  const VerifyEmailFailure(this.message);
  final String message;

  @override
  List<Object?> get props => [message];
}
