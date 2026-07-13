import 'package:geolocator/geolocator.dart';

/// Thin wrapper over geolocator so the map Bloc stays testable. Returns
/// null whenever a location cannot be obtained (service off, permission
/// denied) — the map degrades to a default viewport instead of failing.
class LocationService {
  Future<({double latitude, double longitude})?> getCurrentPosition() async {
    if (!await Geolocator.isLocationServiceEnabled()) return null;

    var permission = await Geolocator.checkPermission();
    if (permission == LocationPermission.denied) {
      permission = await Geolocator.requestPermission();
    }
    if (permission == LocationPermission.denied ||
        permission == LocationPermission.deniedForever) {
      return null;
    }

    final position = await Geolocator.getCurrentPosition();
    return (latitude: position.latitude, longitude: position.longitude);
  }
}
