// ctx:auth_email_password:begin
// ctx:auth_email_password:end
import 'package:app_template/core/result/result.dart';
// ctx:auth_email_password:begin
// ctx:auth_email_password:end
import 'package:app_template/data/repositories/auth_repository.dart';
import 'package:app_template/features/auth/bloc/login_bloc.dart';
// ctx:auth_google:begin
// ctx:auth_google:end
import 'package:app_template/models/user.dart';
import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

// ctx:auth_google:begin
// ctx:auth_google:end

const _user = User(id: 'u1', email: 'a@b.com');

void main() {
  late _MockAuthRepository authRepository;
  // ctx:auth_google:begin
  // ctx:auth_google:end

  setUp(() {
    authRepository = _MockAuthRepository();
    // ctx:auth_google:begin
    // ctx:auth_google:end
  });

  LoginBloc build() => LoginBloc(
    authRepository: authRepository,
    // ctx:auth_google:begin
    // ctx:auth_google:end
  );

  // ctx:auth_email_password:begin
  // ctx:auth_email_password:end

  // ctx:auth_google:begin
  // ctx:auth_google:end
}
