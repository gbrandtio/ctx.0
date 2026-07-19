import 'package:flutter/material.dart';

import 'app/app.dart';
import 'security/rasp_gate.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  // RASP: refuse to run on a compromised device before anything else boots.
  await RaspGate.enforce();
  runApp(const AcmeRoot());
}
