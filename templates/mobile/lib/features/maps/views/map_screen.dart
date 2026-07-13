import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import '../../../core/l10n/l10n.dart';
import '../../../core/widgets/app_header.dart';
import '../bloc/map_bloc.dart';

class MapScreen extends StatelessWidget {
  const MapScreen({super.key});

  /// Neutral fallback viewport when no location is available.
  static const _fallbackCenter = LatLng(0, 0);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppHeader(
        config: HeaderConfig(
          title: (context) => context.l10n.mapTitle,
          showBackButton: false,
          actions: [
            Builder(
              builder: (context) => IconButton(
                icon: const Icon(Icons.refresh),
                tooltip: context.l10n.mapRefresh,
                onPressed: () => context
                    .read<MapBloc>()
                    .add(const MapRefreshRequested()),
              ),
            ),
          ],
        ),
      ),
      body: BlocConsumer<MapBloc, MapState>(
        listenWhen: (previous, current) =>
            current.status == MapStatus.failure &&
            previous.status != MapStatus.failure,
        listener: (context, state) {
          final message = state.errorMessage;
          if (message != null) {
            ScaffoldMessenger.of(context)
              ..hideCurrentSnackBar()
              ..showSnackBar(SnackBar(content: Text(message)));
          }
        },
        builder: (context, state) {
          if (state.status == MapStatus.initial ||
              state.status == MapStatus.loading) {
            return const Center(child: CircularProgressIndicator());
          }

          final center = (state.latitude != null && state.longitude != null)
              ? LatLng(state.latitude!, state.longitude!)
              : _fallbackCenter;

          return Stack(
            children: [
              GoogleMap(
                initialCameraPosition:
                    CameraPosition(target: center, zoom: 13),
                myLocationEnabled:
                    state.status != MapStatus.locationUnavailable,
                myLocationButtonEnabled: true,
                markers: {
                  for (final item in state.items)
                    Marker(
                      markerId: MarkerId(item.id),
                      position: LatLng(item.latitude, item.longitude),
                      infoWindow: InfoWindow(
                        title: item.name,
                        snippet: item.description,
                      ),
                    ),
                },
              ),
              if (state.status == MapStatus.locationUnavailable)
                Positioned(
                  left: 16,
                  right: 16,
                  bottom: 24,
                  child: Card(
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Text(
                        context.l10n.locationUnavailable,
                        textAlign: TextAlign.center,
                      ),
                    ),
                  ),
                ),
            ],
          );
        },
      ),
    );
  }
}
