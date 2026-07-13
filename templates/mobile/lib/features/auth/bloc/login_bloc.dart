import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../data/repositories/auth_repository.dart';
// ctx:auth_google:begin
// ctx:auth_google:end

part 'login_event.dart';
part 'login_state.dart';

/// Login screen Bloc — a Bloc (not Cubit) because several triggers map to
/// one state machine (docs/STATE_MANAGEMENT.md §1). Submissions are
/// droppable: a double-tap can never double-submit (§4). The handlers for
/// each sign-in method sit inside that method's `ctx:` marker block
/// (docs/INTEGRATIONS.md).
class LoginBloc extends Bloc<LoginEvent, LoginState> {
  LoginBloc({
    required AuthRepository authRepository,
    // ctx:auth_google:begin
    // ctx:auth_google:end
  }) : _authRepository = authRepository,
       // ctx:auth_google:begin
       // ctx:auth_google:end
       super(const LoginInitial()) {
    // ctx:auth_email_password:begin
    // ctx:auth_email_password:end
    // ctx:auth_google:begin
    // ctx:auth_google:end
  }

  final AuthRepository _authRepository;
  // ctx:auth_google:begin
  // ctx:auth_google:end

  // ctx:auth_email_password:begin
  // ctx:auth_email_password:end

  // ctx:auth_google:begin
  // ctx:auth_google:end
}
