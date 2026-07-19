import 'package:flutter_bloc/flutter_bloc.dart';

import '../security/ctx_security.dart';
// ctx:anchor:imports
import '../features/auth/bloc/auth_cubit.dart';
import '../features/auth/data/auth_repository.dart';
import '../features/auth/data/token_store.dart';
import '../features/ping/ping_cubit.dart';
import '../features/ping/ping_repository.dart';

/// Composition root: returns the app-wide Bloc providers. Feature overlays
/// register their Bloc/Cubit here by inserting below the anchor marker. The
/// shared [ctxSecureClient] is available to any provider that needs the API.
List<BlocProvider> ctxAppProviders() {
  return <BlocProvider>[
    // ctx:anchor:providers
    BlocProvider<AuthCubit>(create: (_) => AuthCubit(HttpAuthRepository(SecureTokenStore()))..restore()),
    BlocProvider<PingCubit>(create: (_) => PingCubit(PingRepository(ctxSecureClient))),
  ];
}
