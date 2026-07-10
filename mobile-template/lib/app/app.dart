import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../core/config/app_config.dart';
import '../core/l10n/l10n.dart';
import '../core/l10n/locale_cubit.dart';
import '../core/theme/app_theme.dart';
import '../core/theme/theme_cubit.dart';
import '../data/repositories/auth_repository.dart';
import '../data/services/storage/prefs_service.dart';
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
  });

  final List<FeatureModule> modules;
  final AuthRepository authRepository;
  final PrefsService prefs;

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
            );
          },
        ),
      ),
    );
  }
}
