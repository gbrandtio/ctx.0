part of 'login_bloc.dart';

sealed class LoginState extends Equatable {
  const LoginState();

  @override
  List<Object?> get props => [];
}

final class LoginInitial extends LoginState {
  const LoginInitial();
}

final class LoginLoading extends LoginState {
  const LoginLoading();
}

final class LoginSuccess extends LoginState {
  const LoginSuccess();
}

final class LoginFailure extends LoginState {
  const LoginFailure(this.message);
  final String message;

  @override
  List<Object?> get props => [message];
}

// ctx:auth_2fa_email:begin
final class LoginRequiresTwoFactor extends LoginState {
  const LoginRequiresTwoFactor(this.usernameOrEmail, this.password);
  final String usernameOrEmail;
  final String password;

  @override
  List<Object?> get props => [usernameOrEmail, password];
}
// ctx:auth_2fa_email:end
