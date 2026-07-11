import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;

import '../../app/feature_module.dart';
import '../../core/l10n/l10n.dart';
import 'bloc/map_bloc.dart';
import 'data/item_api_service.dart';
import 'data/items_repository.dart';
import 'data/location_service.dart';
import 'views/map_screen.dart';

/// Shipped maps module (optional — delete its line in modules.dart if
/// your product has no spatial surface): Google Map + nearby geo-tagged
/// items (api-template/docs/features/SPATIAL_QUERIES.md).
class MapsModule extends FeatureModule {
  const MapsModule();

  @override
  List<RouteBase> get routes => [
        GoRoute(
          path: '/map',
          builder: (context, state) => BlocProvider(
            create: (context) => MapBloc(
              itemsRepository: context.read<ItemsRepository>(),
              locationService: LocationService(),
            )..add(const MapOpened()),
            child: const MapScreen(),
          ),
        ),
      ];

  @override
  List<RepositoryProvider> get repositories => [
        RepositoryProvider<ItemsRepository>(
          create: (context) => ItemsRepository(
            api: ItemApiService(context.read<http.Client>()),
          ),
        ),
      ];

  @override
  NavItem? get navItem => NavItem(
        rootRoute: '/map',
        icon: Icons.map_outlined,
        selectedIcon: Icons.map,
        label: (context) => context.l10n.mapTitle,
      );
}
