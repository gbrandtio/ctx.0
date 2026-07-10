part of 'login_bloc.dart';

sealed class LoginEvent {
  const LoginEvent();
}

final class LoginSubmitted extends LoginEvent {
  const LoginSubmitted(this.email, this.password);
  final String email;
  final String password;
}

final class LoginWithGooglePressed extends LoginEvent {
  const LoginWithGooglePressed();
}
