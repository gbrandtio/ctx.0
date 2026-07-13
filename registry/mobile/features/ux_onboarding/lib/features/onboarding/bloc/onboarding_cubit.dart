// ctx:ux_onboarding:begin
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../data/services/storage/prefs_service.dart';

class OnboardingCubit extends Cubit<int> {
  OnboardingCubit({required this.prefsService, required this.onComplete})
    : super(0);

  final PrefsService prefsService;
  final void Function() onComplete;

  void setPage(int index) {
    emit(index);
  }

  Future<void> completeOnboarding() async {
    await prefsService.setOnboardingDone(true);
    onComplete();
  }
}

// ctx:ux_onboarding:end
