import 'dart:io';

void main() {
  final file = File('packages/ctx0_cli/lib/src/create.dart');
  var text = file.readAsStringSync();

  // Remove the check for integrations.json
  text = text.replaceAll(
    "if (!File('${templateDir.path}/.ctx/integrations.json').existsSync()) {\n"
    "    stderr.writeln('error: ${templateDir.path} is not a ctx template '\n"
    "        '(no .ctx/integrations.json).');\n"
    "    return 2;\n"
    "  }",
    "// No integrations.json check"
  );
  
  // Inject workspace.json writing before manifest.json
  text = text.replaceAll(
    "// ---- Manifest ----",
    "// ---- Workspace & Manifest ----\n"
    "  File('${outDir.path}/.ctx/workspace.json').writeAsStringSync(\n"
    "      '{\"kind\": \"mobile\", \"enabledFeatures\": []}\\n');\n"
  );
  text = text.replaceAll(
    "// ---- Manifest ----", // for the api template
    "// ---- Workspace & Manifest ----\n"
    "  File('${outDir.path}/.ctx/workspace.json').writeAsStringSync(\n"
    "      '{\"kind\": \"api\", \"enabledFeatures\": []}\\n');\n"
  );

  // Update repo loading
  text = text.replaceAll(
    "final repo = InjectorRepo(outDir, Catalog.load(outDir));",
    "final repo = await openRepo(outDir);" // Already sed'd to this? We will ensure it works.
  );

  file.writeAsStringSync(text);
}
