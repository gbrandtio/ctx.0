import Flutter
// ctx:maps_google:begin
import GoogleMaps
// ctx:maps_google:end
import UIKit

@main
@objc class AppDelegate: FlutterAppDelegate {
  override func application(
    _ application: UIApplication,
    didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?
  ) -> Bool {
    // Maps module: the key is read from Info.plist (GMSApiKey), which the
    // build populates from MAPS_API_KEY (docs/ENVIRONMENT_VARIABLES.md).
    // Missing key: the app runs; only the map screen is non-functional.
    // The ctx marker blocks are managed by tool/scaffold
    // (docs/INTEGRATIONS.md) — never edit them by hand.
    // ctx:maps_google:begin
    if let mapsApiKey = Bundle.main.object(forInfoDictionaryKey: "GMSApiKey") as? String,
       !mapsApiKey.isEmpty {
      GMSServices.provideAPIKey(mapsApiKey)
    }
    // ctx:maps_google:end
    GeneratedPluginRegistrant.register(with: self)
    return super.application(application, didFinishLaunchingWithOptions: launchOptions)
  }
}
