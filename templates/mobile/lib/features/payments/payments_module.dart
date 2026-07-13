import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;

import '../../app/feature_module.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import 'bloc/checkout_bloc.dart';
import 'data/payment_api_service.dart';
import 'data/payments_repository.dart';
import 'data/stripe_service.dart';
import 'views/checkout_screen.dart';

/// Shipped payments module: order-based checkout via Stripe PaymentSheet
/// (cards, Google Pay, Apple Pay). No nav item — business features push
/// `/checkout/:orderId` with a server-issued order.
class PaymentsModule extends FeatureModule {
  const PaymentsModule();

  @override
  List<RouteBase> get routes => [
        GoRoute(
          path: '/checkout/:orderId',
          builder: (context, state) => BlocProvider(
            create: (context) => CheckoutBloc(
              repository: context.read<PaymentsRepository>(),
            ),
            child: CheckoutScreen(
              orderId: state.pathParameters['orderId']!,
            ),
          ),
        ),
      ];

  @override
  List<RepositoryProvider> get repositories => [
        RepositoryProvider<PaymentsRepository>(
          create: (context) => PaymentsRepository(
            api: PaymentApiService(context.read<http.Client>()),
            stripe: StripeService(),
            cachingClient: context.read<CachingClient>(),
          ),
        ),
      ];
}
