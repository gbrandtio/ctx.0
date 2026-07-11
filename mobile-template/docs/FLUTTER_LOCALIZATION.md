# Localization - Internationalizing Flutter Apps

**Source:** [docs.flutter.dev/ui/internationalization](https://docs.flutter.dev/ui/internationalization)

This document details the workflows and concepts necessary to localize a Flutter application using `MaterialApp` or `CupertinoApp`.

## 1\. Initial Setup and Dependencies

By default, Flutter only provides US English localizations. To add support for other languages, you must include the `flutter_localizations` package.

### Add Dependencies

Run the following commands to add the necessary packages:

```bash
flutter pub add flutter_localizations --sdk=flutter
flutter pub add intl:any
```

Ensure your `pubspec.yaml` looks like this:

```yaml
dependencies:
  flutter:
    sdk: flutter
  flutter_localizations:
    sdk: flutter
  intl: any

# Enable generation in the flutter section
flutter:
  generate: true
```

## 2\. Configuration (l10n.yaml)

Add a `l10n.yaml` file to the root of your project to configure the localization tool.

**File:** `root/l10n.yaml`

```yaml
arb-dir: lib/core/l10n
template-arb-file: app_en.arb
output-localization-file: app_localizations.dart
```

> **Note:** Recent Flutter versions generate the localization code into the ARB directory (`lib/core/l10n/` here) (the deprecated "synthetic package" `package:flutter_gen` is no longer used). Import the generated file with a normal package import, e.g. `import 'package:your_app/core/l10n/app_localizations.dart';`.

## 3\. Creating ARB Files

The App Resource Bundle (`.arb`) files contain the localized resources. Place them in `lib/core/l10n`.

### Basic String

**File:** `lib/core/l10n/app_en.arb` (English Template)

```json
{
  "helloWorld": "Hello World!",
  "@helloWorld": {
    "description": "The conventional newborn programmer greeting"
  }
}
```

**File:** `lib/core/l10n/app_es.arb` (Spanish)

```json
{
  "helloWorld": "¡Hola Mundo!"
}
```

### Placeholders, Plurals, and Selects

You can define complex messages directly in the `.arb` file.

**Placeholders:**

```json
{
  "hello": "Hello {userName}",
  "@hello": {
    "description": "A message with a single parameter",
    "placeholders": {
      "userName": {
        "type": "String",
        "example": "Bob"
      }
    }
  }
}
```

**Plurals:**

```json
{
  "nWombats": "{count, plural, =0{No wombats} =1{1 wombat} other{{count} wombats}}",
  "@nWombats": {
    "description": "A plural message",
    "placeholders": {
      "count": {
        "type": "int",
        "format": "compact"
      }
    }
  }
}
```

**Selects (Gender):**

```json
{
  "pronoun": "{gender, select, male{he} female{she} other{they}}",
  "@pronoun": {
    "placeholders": {
      "gender": {
        "type": "String"
      }
    }
  }
}
```

**Escaping Syntax:**
To use `{` and `}` characters literally in your text, escape them using single quotes:

```json
{
  "price": "The price is '{'{price}'}'"
}
```

## 4\. Dart Implementation

After creating the `.arb` files, run `flutter run` or `flutter gen-l10n` to generate the Dart code.

### MaterialApp Configuration

Import the generated localizations file and configure the `MaterialApp` or `CupertinoApp`.

```dart
import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:your_app/core/l10n/app_localizations.dart';

void main() {
  runApp(const MyApp());
}

class MyApp extends StatelessWidget {
  const MyApp({super.key});

  @override
  Widget build(BuildContext context) {
    return const MaterialApp(
      title: 'Localizations Sample App',
      // Auto-generated delegates and supported locales
      localizationsDelegates: AppLocalizations.localizationsDelegates,
      supportedLocales: AppLocalizations.supportedLocales,
      home: MyHomePage(),
    );
  }
}
```

*Note: `AppLocalizations.localizationsDelegates` automatically includes:*

  * `AppLocalizations.delegate`
  * `GlobalMaterialLocalizations.delegate`
  * `GlobalWidgetsLocalizations.delegate`
  * `GlobalCupertinoLocalizations.delegate`

### Using Localized Values

Access the generated getters via the `BuildContext`.

```dart
// Basic Usage
Text(AppLocalizations.of(context)!.helloWorld)

// With Placeholders
Text(AppLocalizations.of(context)!.hello("Bob"))

// With Plurals
Text(AppLocalizations.of(context)!.nWombats(1))
```

## 5\. Platform Specifics: iOS

For iOS, you must update the `Info.plist` file to support the correct locales. This ensures system dialogs (like permission requests) appear in the correct language.

**File:** `ios/Runner/Info.plist`

```xml
<key>CFBundleLocalizations</key>
<array>
    <string>en</string>
    <string>es</string>
</array>
```

## 6\. Advanced Topics

### Overriding the Locale

If a specific part of your app needs to be in a different locale than the system default (or the rest of the app), use `Localizations.override`.

```dart
Localizations.override(
  context: context,
  locale: const Locale('es'),
  child: Builder(
    builder: (context) {
      // This widget and its children will use the Spanish locale
      return CalendarDatePicker(
        initialDate: DateTime.now(),
        firstDate: DateTime(1900),
        lastDate: DateTime(2100),
        onDateChanged: (value) {},
      );
    },
  ),
);
```

### Loading Messages without Context

If you need to load localized strings outside of the widget tree (no `BuildContext` available), you generally cannot use the generated `AppLocalizations.of(context)`. You must structure your app to pass the strings down or use a state management solution that holds the current locale.

### Handling Numbers and Dates

The generated `AppLocalizations` code uses the `intl` package formatted methods automatically if you define types in your ARB placeholders:

  * **Numbers:** Use `type: int` or `double` and optional `format` key (e.g., `compactCurrency`, `decimalPattern`).
  * **Dates:** Use `type: DateTime` and `format` key (e.g., `yMd`).