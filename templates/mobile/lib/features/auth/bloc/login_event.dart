part of 'login_bloc.dart';

sealed class LoginEvent {
  const LoginEvent();
}

// ctx:auth_email_password:begin
final class LoginSubmitted extends LoginEvent {
  const LoginSubmitted(this.email, this.password);
  final String email;
  final String password;
}
// ctx:auth_email_password:end

// ctx:auth_google:begin
final class LoginWithGooglePressed extends LoginEvent {
  const LoginWithGooglePressed();
}
// ctx:auth_google:end
