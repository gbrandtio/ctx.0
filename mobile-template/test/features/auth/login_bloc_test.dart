import 'package:app_template/core/models/problem_details.dart';
import 'package:app_template/core/result/result.dart';
import 'package:app_template/core/utils/app_exception.dart';
import 'package:app_template/data/repositories/auth_repository.dart';
import 'package:app_template/features/auth/bloc/login_bloc.dart';
import 'package:app_template/features/auth/data/google_auth_service.dart';
import 'package:app_template/models/user.dart';
import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockGoogleAuth extends Mock implements GoogleAuthService {}

const _user = User(id: 'u1', email: 'a@b.com');

void main() {
  late _MockAuthRepository authRepository;
  late _MockGoogleAuth googleAuth;

  setUp(() {
    authRepository = _MockAuthRepository();
    googleAuth = _MockGoogleAuth();
  });

  LoginBloc build() =>
      LoginBloc(authRepository: authRepository, googleAuth: googleAuth);

  blocTest<LoginBloc, LoginState>(
    'emits [loading, success] on accepted credentials',
    build: () {
      when(() => authRepository.login(any(), any()))
          .thenAnswer((_) async => const Result.success(_user));
      return build();
    },
    act: (bloc) => bloc.add(const LoginSubmitted('a@b.com', 'pw')),
    expect: () => const [LoginLoading(), LoginSuccess()],
  );

  blocTest<LoginBloc, LoginState>(
    'emits [loading, failure] with the client-safe message when rejected',
    build: () {
      when(() => authRepository.login(any(), any())).thenAnswer(
        (_) async => const Result.failure(
          AppException(
            ProblemDetails(status: 400, detail: 'Invalid credentials.'),
          ),
        ),
      );
      return build();
    },
    act: (bloc) => bloc.add(const LoginSubmitted('a@b.com', 'wrong')),
    expect: () => const [
      LoginLoading(),
      LoginFailure('Invalid credentials.'),
    ],
  );

  blocTest<LoginBloc, LoginState>(
    'double-tap cannot double-submit (droppable transformer)',
    build: () {
      when(() => authRepository.login(any(), any())).thenAnswer((_) async {
        await Future<void>.delayed(const Duration(milliseconds: 10));
        return const Result.success(_user);
      });
      return build();
    },
    act: (bloc) => bloc
      ..add(const LoginSubmitted('a@b.com', 'pw'))
      ..add(const LoginSubmitted('a@b.com', 'pw')),
    wait: const Duration(milliseconds: 50),
    expect: () => const [LoginLoading(), LoginSuccess()],
    verify: (_) => verify(() => authRepository.login(any(), any())).called(1),
  );

  blocTest<LoginBloc, LoginState>(
    'Google flow returns to initial when the user cancels',
    build: () {
      when(() => googleAuth.signIn()).thenAnswer((_) async => null);
      return build();
    },
    act: (bloc) => bloc.add(const LoginWithGooglePressed()),
    expect: () => const [LoginLoading(), LoginInitial()],
    verify: (_) =>
        verifyNever(() => authRepository.signInWithGoogle(any())),
  );

  blocTest<LoginBloc, LoginState>(
    'Google flow exchanges the ID token for a session',
    build: () {
      when(() => googleAuth.signIn()).thenAnswer((_) async => 'id-token');
      when(() => authRepository.signInWithGoogle('id-token'))
          .thenAnswer((_) async => const Result.success(_user));
      return build();
    },
    act: (bloc) => bloc.add(const LoginWithGooglePressed()),
    expect: () => const [LoginLoading(), LoginSuccess()],
  );
}
