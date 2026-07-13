import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;

import '../core/config/app_config.dart';
import '../core/l10n/l10n.dart';
import '../core/l10n/locale_cubit.dart';
import '../core/theme/app_theme.dart';
import '../core/theme/theme_cubit.dart';
import '../data/repositories/auth_repository.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import '../data/services/storage/prefs_service.dart';
import '../core/utils/time_provider.dart';
import '../core/utils/logging_service.dart';
// ctx:settings:begin
import '../features/settings/views/widgets/gdpr_banner_overlay.dart';
// ctx:settings:end
// ctx:app_updates:begin
import '../features/app_updates/app_updates_module.dart';
import '../features/app_updates/views/app_updates_overlay.dart';
// ctx:app_updates:end
import 'feature_module.dart';
import 'router.dart';

/// The application shell (docs/APP_SHELL.md): composes DI, global Blocs,
/// theme/locale binding, and the module-assembled router. Products
/// configure it via lib/app/modules.dart and lib/core/config/ — this file
/// is not edited per product.
class App extends StatefulWidget {
  const App({
    super.key,
    required this.modules,
    required this.authRepository,
    required this.prefs,
    required this.apiClient,
    required this.cachingClient,
    required this.timeProvider,
    required this.loggingService,
  });

  final List<FeatureModule> modules;
  final AuthRepository authRepository;
  final PrefsService prefs;
  final TimeProvider timeProvider;
  final LoggingService loggingService;

  /// The fully-intercepted HTTP client (docs/HTTP_HANDLING.md); modules
  /// build their ApiServices from it.
  final http.Client apiClient;

  /// Provided as [CachingClient] so modules can perform event-driven cache invalidation.
  final CachingClient cachingClient;

  @override
  State<App> createState() => _AppState();
}

class _AppState extends State<App> {
  late final GoRouter _router;

  @override
  void initState() {
    super.initState();
    _router = buildRouter(
      modules: widget.modules,
      authRepository: widget.authRepository,
    );
  }

  @override
  Widget build(BuildContext context) {
    return MultiRepositoryProvider(
      providers: [
        RepositoryProvider<AuthRepository>.value(value: widget.authRepository),
        RepositoryProvider<PrefsService>.value(value: widget.prefs),
        RepositoryProvider<http.Client>.value(value: widget.apiClient),
        RepositoryProvider<CachingClient>.value(value: widget.cachingClient),
        RepositoryProvider<TimeProvider>.value(value: widget.timeProvider),
        RepositoryProvider<LoggingService>.value(value: widget.loggingService),
        RepositoryProvider<ModuleRegistry>.value(
          value: ModuleRegistry(widget.modules),
        ),
        for (final module in widget.modules) ...module.repositories,
      ],
      child: MultiBlocProvider(
        providers: [
          BlocProvider<ThemeCubit>(
            create: (context) => ThemeCubit(
              prefs: widget.prefs,
              authRepository: widget.authRepository,
            ),
          ),
          BlocProvider<LocaleCubit>(
            create: (context) => LocaleCubit(
              prefs: widget.prefs,
              authRepository: widget.authRepository,
            ),
          ),
          for (final module in widget.modules) ...?module.globalBlocs,
        ],
        child: Builder(
          builder: (context) {
            final themeMode = context.watch<ThemeCubit>().state;
            final locale = context.watch<LocaleCubit>().state;
            return MaterialApp.router(
              title: AppConfig.appName,
              theme: AppTheme.light,
              darkTheme: AppTheme.dark,
              themeMode: themeMode,
              locale: locale,
              localizationsDelegates: AppLocalizations.localizationsDelegates,
              supportedLocales: AppLocalizations.supportedLocales,
              routerConfig: _router,
              builder: (context, child) {
                Widget wrapped = child!;
// ctx:settings:begin
                wrapped = GdprBannerOverlay(child: wrapped);
// ctx:settings:end
// ctx:app_updates:begin
                wrapped = AppUpdatesOverlay(
                  notifier: updateRequiredNotifier,
                  child: wrapped,
                );
// ctx:app_updates:end
                return wrapped;
              },
            );
          },
        ),
      ),
    );
  }
}
