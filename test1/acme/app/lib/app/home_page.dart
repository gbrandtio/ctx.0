import 'package:flutter/material.dart';
// ctx:anchor:home-imports
import '../features/ping/views/ping_page.dart';

/// The app's landing screen. Feature overlays register a navigation tile by
/// inserting below the `home-tiles` anchor.
class CtxHomePage extends StatelessWidget {
  const CtxHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final tiles = <Widget>[
      // ctx:anchor:home-tiles
      ListTile(title: const Text('Secure ping'), leading: const Icon(Icons.lock), onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const PingPage()))),
    ];
    return Scaffold(
      appBar: AppBar(title: const Text('Acme')),
      body: tiles.isEmpty ? const Center(child: Text('Acme')) : ListView(children: tiles),
    );
  }
}
