// ctx:ux_onboarding:begin
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../app/feature_module.dart';
import '../../data/services/storage/prefs_service.dart';
import 'bloc/onboarding_cubit.dart';
import 'views/onboarding_screen.dart';

class OnboardingModule extends FeatureModule {
  const OnboardingModule();

  @override
  List<String> get publicRoutePaths => ['/onboarding'];

  @override
  List<GoRoute> get routes => [
    GoRoute(
      path: '/onboarding',
      builder: (context, state) {
        return BlocProvider(
          create: (context) => OnboardingCubit(
            prefsService: context.read<PrefsService>(),
            onComplete: () => context.go('/'),
          ),
          child: const OnboardingScreen(),
        );
      },
    ),
  ];
}

// ctx:ux_onboarding:end
