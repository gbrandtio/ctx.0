part of 'signup_bloc.dart';

sealed class SignupState extends Equatable {
  const SignupState();

  @override
  List<Object?> get props => [];
}

final class SignupInitial extends SignupState {
  const SignupInitial();
}

final class SignupLoading extends SignupState {
  const SignupLoading();
}

/// The code was emailed; the UI navigates to the verify screen carrying
/// [pending] so registration can complete with the entered code.
final class SignupCodeSent extends SignupState {
  const SignupCodeSent(this.pending);
  final PendingRegistration pending;

  @override
  List<Object?> get props => [pending];
}

final class SignupFailure extends SignupState {
  const SignupFailure(this.message);
  final String message;

  @override
  List<Object?> get props => [message];
}
