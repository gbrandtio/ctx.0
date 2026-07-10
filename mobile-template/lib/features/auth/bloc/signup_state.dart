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

final class SignupSuccess extends SignupState {
  const SignupSuccess();
}

final class SignupFailure extends SignupState {
  const SignupFailure(this.message);
  final String message;

  @override
  List<Object?> get props => [message];
}
