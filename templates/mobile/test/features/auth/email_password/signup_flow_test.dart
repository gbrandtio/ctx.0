import 'package:app_template/models/auth_session.dart';
import 'package:app_template/core/models/problem_details.dart';
import 'package:app_template/core/result/result.dart';
import 'package:app_template/core/utils/app_exception.dart';
import 'package:app_template/core/utils/time_provider.dart';
import 'package:app_template/data/repositories/auth_repository.dart';
import 'package:app_template/features/auth/email_password/bloc/signup_bloc.dart';
import 'package:app_template/features/auth/email_password/bloc/verify_email_cubit.dart';
import 'package:app_template/features/auth/email_password/data/pending_registration.dart';
import 'package:app_template/models/user.dart';
import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

const _user = User(id: 'u1', email: 'new@example.com');

void main() {
  late _MockAuthRepository authRepository;

  setUp(() => authRepository = _MockAuthRepository());

  group('SignupBloc (step 1: request code)', () {
    blocTest<SignupBloc, SignupState>(
      'requesting a code emits SignupCodeSent carrying the pending data',
      build: () {
        when(
          () => authRepository.sendSignupCode('new@example.com'),
        ).thenAnswer((_) async => const Result.success(null));
        return SignupBloc(
          authRepository: authRepository,
          timeProvider: const SystemTimeProvider(),
        );
      },
      act: (bloc) => bloc.add(
        const SignupSubmitted(
          email: 'new@example.com',
          password: 's3cur3P@ss',
          displayName: 'New User',
          consents: {'terms_and_privacy': true},
        ),
      ),
      expect: () => [
        const SignupLoading(),
        isA<SignupCodeSent>()
            .having((s) => s.pending.email, 'email', 'new@example.com')
            .having(
              (s) => s.pending.username,
              'derived username',
              startsWith('new'),
            )
            .having((s) => s.pending.displayName, 'name', 'New User'),
      ],
    );

    blocTest<SignupBloc, SignupState>(
      'a send-code failure surfaces a client-safe message',
      build: () {
        when(() => authRepository.sendSignupCode(any())).thenAnswer(
          (_) async => const Result.failure(
            AppException(
              ProblemDetails(status: 409, detail: 'Email already exists.'),
            ),
          ),
        );
        return SignupBloc(
          authRepository: authRepository,
          timeProvider: const SystemTimeProvider(),
        );
      },
      act: (bloc) => bloc.add(
        const SignupSubmitted(
          email: 'taken@example.com',
          password: 's3cur3P@ss',
          consents: {},
        ),
      ),
      expect: () => const [
        SignupLoading(),
        SignupFailure('Email already exists.'),
      ],
    );
  });

  group('VerifyEmailCubit (step 2: register with code)', () {
    const pending = PendingRegistration(
      username: 'new1234',
      email: 'new@example.com',
      password: 's3cur3P@ss',
      consents: {'terms_and_privacy': true},
    );

    blocTest<VerifyEmailCubit, VerifyEmailState>(
      'submitting the code registers with the pending data and verifies',
      build: () {
        when(
          () => authRepository.register(
            username: 'new1234',
            email: 'new@example.com',
            password: 's3cur3P@ss',
            verificationCode: '123456',
            displayName: null,
            consents: {'terms_and_privacy': true},
          ),
        ).thenAnswer((_) async => const Result.success(AuthSession(user: _user)));
        return VerifyEmailCubit(
          authRepository: authRepository,
          pending: pending,
        );
      },
      act: (cubit) => cubit.verify('123456'),
      expect: () => const [VerifyEmailSubmitting(), VerifyEmailVerified()],
    );

    blocTest<VerifyEmailCubit, VerifyEmailState>(
      'resend re-requests a code for the pending email',
      build: () {
        when(
          () => authRepository.sendSignupCode('new@example.com'),
        ).thenAnswer((_) async => const Result.success(null));
        return VerifyEmailCubit(
          authRepository: authRepository,
          pending: pending,
        );
      },
      act: (cubit) => cubit.resend(),
      expect: () => const [VerifyEmailSubmitting(), VerifyEmailResent()],
      verify: (_) => verify(
        () => authRepository.sendSignupCode('new@example.com'),
      ).called(1),
    );

    blocTest<VerifyEmailCubit, VerifyEmailState>(
      'a wrong code surfaces the API message',
      build: () {
        when(
          () => authRepository.register(
            username: any(named: 'username'),
            email: any(named: 'email'),
            password: any(named: 'password'),
            verificationCode: any(named: 'verificationCode'),
            displayName: any(named: 'displayName'),
            consents: any(named: 'consents'),
          ),
        ).thenAnswer(
          (_) async => const Result.failure(
            AppException(
              ProblemDetails(
                status: 400,
                detail: 'Invalid or expired verification code.',
              ),
            ),
          ),
        );
        return VerifyEmailCubit(
          authRepository: authRepository,
          pending: pending,
        );
      },
      act: (cubit) => cubit.verify('000000'),
      expect: () => const [
        VerifyEmailSubmitting(),
        VerifyEmailFailure('Invalid or expired verification code.'),
      ],
    );
  });
}
