import 'package:flutter_bloc/flutter_bloc.dart';

import '../security/ctx_security.dart';
// ctx:anchor:imports

/// Composition root: returns the app-wide Bloc providers. Feature overlays
/// register their Bloc/Cubit here by inserting below the anchor marker. The
/// shared [ctxSecureClient] is available to any provider that needs the API.
List<BlocProvider> ctxAppProviders() {
  return <BlocProvider>[
    // ctx:anchor:providers
  ];
}
