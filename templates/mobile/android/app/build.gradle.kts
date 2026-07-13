import java.util.Properties

plugins {
    id("com.android.application")
    id("kotlin-android")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Release signing (docs/ENVIRONMENT_VARIABLES.md §1): values come from
// system environment variables or android/key.properties (never committed).
val keyProperties = Properties().apply {
    val file = rootProject.file("key.properties")
    if (file.exists()) file.inputStream().use { load(it) }
}

fun signingValue(name: String): String? =
    System.getenv(name) ?: keyProperties.getProperty(name)

android {
    namespace = "com.example.app_template"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = JavaVersion.VERSION_17.toString()
    }

    defaultConfig {
        // TODO: Specify your own unique Application ID (https://developer.android.com/studio/build/application-id.html).
        applicationId = "com.example.app_template"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        versionCode = flutter.versionCode
        versionName = flutter.versionName

        // Consumed by the AndroidManifest for the maps module
        // (docs/ENVIRONMENT_VARIABLES.md).
        manifestPlaceholders["MAPS_API_KEY"] = System.getenv("MAPS_API_KEY") ?: ""
    }

    signingConfigs {
        if (signingValue("RELEASE_STORE_FILE") != null) {
            create("release") {
                storeFile = file(signingValue("RELEASE_STORE_FILE")!!)
                storePassword = signingValue("RELEASE_STORE_PASSWORD")
                keyAlias = signingValue("RELEASE_KEY_ALIAS")
                keyPassword = signingValue("RELEASE_KEY_PASSWORD")
            }
        }
    }

    buildTypes {
        release {
            // Falls back to debug keys so `flutter run --release` works
            // before release signing is configured.
            signingConfig = signingConfigs.findByName("release")
                ?: signingConfigs.getByName("debug")
        }
    }
}

dependencies {
    // MaterialComponents theme host: styles.xml's Launch/Normal themes
    // inherit Theme.MaterialComponents (also required by the Stripe
    // PaymentSheet) — permanent, not integration-scoped.
    implementation("com.google.android.material:material:1.12.0")
}

flutter {
    source = "../.."
}
