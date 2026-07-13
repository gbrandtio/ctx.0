import 'package:bloc/bloc.dart';
import 'package:equatable/equatable.dart';

import '../../../../core/utils/app_exception.dart';
import '../../../../data/repositories/auth_repository.dart';
import '../../../../data/services/api/user_api_service.dart';

part 'two_factor_state.dart';

class TwoFactorCubit extends Cubit<TwoFactorState> {
  TwoFactorCubit({
    required AuthRepository authRepository,
    required UserApiService userApi,
  }) : _authRepository = authRepository,
       _userApi = userApi,
       super(const TwoFactorInitial());

  final AuthRepository _authRepository;
  final UserApiService _userApi;

  Future<void> submitCode(String usernameOrEmail, String password, String code) async {
    emit(const TwoFactorLoading());
    try {
      final session = await _userApi.authenticate2FA(usernameOrEmail, password, code);
      // Once authenticated via 2FA, establish the session properly
      await _authRepository.establishSessionWith(session);
      emit(const TwoFactorSuccess());
    } on Exception catch (e) {
      emit(TwoFactorFailure(AppException.from(e).userFriendlyMessage));
    }
  }
}
