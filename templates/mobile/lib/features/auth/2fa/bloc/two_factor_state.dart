part of 'two_factor_cubit.dart';

sealed class TwoFactorState extends Equatable {
  const TwoFactorState();

  @override
  List<Object?> get props => [];
}

final class TwoFactorInitial extends TwoFactorState {
  const TwoFactorInitial();
}

final class TwoFactorLoading extends TwoFactorState {
  const TwoFactorLoading();
}

final class TwoFactorSuccess extends TwoFactorState {
  const TwoFactorSuccess();
}

final class TwoFactorFailure extends TwoFactorState {
  const TwoFactorFailure(this.message);
  final String message;

  @override
  List<Object?> get props => [message];
}
