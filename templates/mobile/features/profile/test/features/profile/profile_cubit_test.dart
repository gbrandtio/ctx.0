import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/features/profile/bloc/profile_cubit.dart';
import 'package:ctxapp/features/profile/data/profile_repository.dart';

/// In-memory fake so the cubit can be tested without HTTP.
class FakeProfileRepository implements ProfileRepository {
  FakeProfileRepository(this._data);

  ProfileData _data;

  @override
  Future<ProfileData> get() async => _data;

  @override
  Future<ProfileData> update({required String displayName, String? bio, String? avatarUrl}) async {
    _data = ProfileData(displayName: displayName, bio: bio, avatarUrl: avatarUrl);
    return _data;
  }
}

void main() {
  blocTest<ProfileCubit, ProfileState>(
    'load fetches the current profile',
    build: () => ProfileCubit(FakeProfileRepository(const ProfileData(displayName: 'Ada'))),
    act: (cubit) => cubit.load(),
    expect: () => [
      const ProfileState(status: ProfileStatus.loading),
      isA<ProfileState>()
          .having((s) => s.status, 'status', ProfileStatus.ready)
          .having((s) => s.profile?.displayName, 'displayName', 'Ada'),
    ],
  );

  blocTest<ProfileCubit, ProfileState>(
    'save updates the profile and emits it',
    build: () => ProfileCubit(FakeProfileRepository(const ProfileData(displayName: 'Ada'))),
    act: (cubit) => cubit.save(displayName: 'Ada Lovelace', bio: 'Analytical.'),
    expect: () => [
      const ProfileState(status: ProfileStatus.saving),
      isA<ProfileState>()
          .having((s) => s.status, 'status', ProfileStatus.ready)
          .having((s) => s.profile?.displayName, 'displayName', 'Ada Lovelace')
          .having((s) => s.profile?.bio, 'bio', 'Analytical.'),
    ],
  );
}
