part of 'profile_cubit.dart';

enum ProfileStatus { idle, refreshing, saving, saveSuccess, failure }

class ProfileState extends Equatable {
  const ProfileState({
    this.status = ProfileStatus.idle,
    this.user,
    this.errorMessage,
  });

  factory ProfileState.from(AuthState authState) => ProfileState(
        user: authState is Authenticated ? authState.user : null,
      );

  final ProfileStatus status;
  final User? user;
  final String? errorMessage;

  ProfileState copyWith({
    ProfileStatus? status,
    User? user,
    String? errorMessage,
  }) {
    return ProfileState(
      status: status ?? this.status,
      user: user ?? this.user,
      errorMessage: errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props => [status, user, errorMessage];
}
