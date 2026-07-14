// ctx:ux_onboarding:begin
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:permission_handler/permission_handler.dart';

import '../bloc/onboarding_cubit.dart';

class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key});

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  late final PageController _pageController;

  @override
  void initState() {
    super.initState();
    _pageController = PageController();
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  void _nextPage() {
    _pageController.nextPage(
      duration: const Duration(milliseconds: 300),
      curve: Curves.easeInOut,
    );
  }

  @override
  Widget build(BuildContext context) {
    // Collect the slides. Using dynamic collection allows us to conditionally
    // inject permission slides based on what integrations are enabled.
    final slides = <Widget>[
      _WelcomeSlide(onNext: _nextPage),
      // ctx:maps_google:begin
      _LocationSlide(onNext: _nextPage),
      // ctx:maps_google:end
      // ctx:image_capture:begin
      _CameraSlide(onNext: _nextPage),
      // ctx:image_capture:end
      // ctx:push_firebase:begin
      _PushSlide(onNext: _nextPage),
      // ctx:push_firebase:end
      _FinalSlide(
        onComplete: () => context.read<OnboardingCubit>().completeOnboarding(),
      ),
    ];

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: PageView(
                controller: _pageController,
                onPageChanged: (index) =>
                    context.read<OnboardingCubit>().setPage(index),
                children: slides,
              ),
            ),
            Padding(
              padding: const EdgeInsets.all(24.0),
              child: BlocBuilder<OnboardingCubit, int>(
                builder: (context, currentPage) {
                  return Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: List.generate(
                      slides.length,
                      (index) => AnimatedContainer(
                        duration: const Duration(milliseconds: 300),
                        margin: const EdgeInsets.symmetric(horizontal: 4.0),
                        height: 8.0,
                        width: currentPage == index ? 24.0 : 8.0,
                        decoration: BoxDecoration(
                          color: currentPage == index
                              ? Theme.of(context).colorScheme.primary
                              : Theme.of(
                                  context,
                                ).colorScheme.surfaceContainerHighest,
                          borderRadius: BorderRadius.circular(4.0),
                        ),
                      ),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _WelcomeSlide extends StatelessWidget {
  const _WelcomeSlide({required this.onNext});

  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return _BaseSlide(
      icon: Icons.waving_hand_rounded,
      title: 'Welcome to App',
      description:
          'This is your brand new application. Swipe through to learn what you can do and set things up.',
      buttonText: 'Next',
      onButtonPressed: onNext,
    );
  }
}

// ctx:maps_google:begin
class _LocationSlide extends StatelessWidget {
  const _LocationSlide({required this.onNext});

  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return _BaseSlide(
      icon: Icons.location_on_rounded,
      title: 'Enable Location',
      description:
          'We use your location to show relevant content around you. You can change this later.',
      buttonText: 'Grant Permission',
      onButtonPressed: () async {
        await Permission.location.request();
        onNext();
      },
    );
  }
}
// ctx:maps_google:end

// ctx:image_capture:begin
class _CameraSlide extends StatelessWidget {
  const _CameraSlide({required this.onNext});

  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return _BaseSlide(
      icon: Icons.camera_alt_rounded,
      title: 'Enable Camera',
      description:
          'Capture moments and upload photos directly. We need camera access to do this.',
      buttonText: 'Grant Permission',
      onButtonPressed: () async {
        await Permission.camera.request();
        onNext();
      },
    );
  }
}
// ctx:image_capture:end

// ctx:push_firebase:begin
class _PushSlide extends StatelessWidget {
  const _PushSlide({required this.onNext});

  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return _BaseSlide(
      icon: Icons.notifications_active_rounded,
      title: 'Stay Notified',
      description:
          'Get push notifications for important updates. Don\'t miss out!',
      buttonText: 'Grant Permission',
      onButtonPressed: () async {
        await Permission.notification.request();
        onNext();
      },
    );
  }
}
// ctx:push_firebase:end

class _FinalSlide extends StatelessWidget {
  const _FinalSlide({required this.onComplete});

  final VoidCallback onComplete;

  @override
  Widget build(BuildContext context) {
    return _BaseSlide(
      icon: Icons.check_circle_rounded,
      title: 'You\'re All Set!',
      description: 'Let\'s get started.',
      buttonText: 'Let\'s Go',
      onButtonPressed: onComplete,
    );
  }
}

class _BaseSlide extends StatelessWidget {
  const _BaseSlide({
    required this.icon,
    required this.title,
    required this.description,
    required this.buttonText,
    required this.onButtonPressed,
  });

  final IconData icon;
  final String title;
  final String description;
  final String buttonText;
  final VoidCallback onButtonPressed;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 32.0),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(icon, size: 80.0, color: theme.colorScheme.primary),
          const SizedBox(height: 32.0),
          Text(
            title,
            style: theme.textTheme.headlineMedium?.copyWith(
              fontWeight: FontWeight.bold,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 16.0),
          Text(
            description,
            style: theme.textTheme.bodyLarge?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 48.0),
          FilledButton(
            onPressed: onButtonPressed,
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(56.0),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(16.0),
              ),
            ),
            child: Text(
              buttonText,
              style: const TextStyle(
                fontSize: 16.0,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

// ctx:ux_onboarding:end
