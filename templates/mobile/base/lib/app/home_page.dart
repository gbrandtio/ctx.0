import 'package:flutter/material.dart';
// ctx:anchor:home-imports

/// The app's landing screen. Feature overlays register a navigation tile by
/// inserting below the `home-tiles` anchor.
class CtxHomePage extends StatelessWidget {
  const CtxHomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final tiles = <Widget>[
      // ctx:anchor:home-tiles
    ];
    return Scaffold(
      appBar: AppBar(title: const Text('CtxApp')),
      body: tiles.isEmpty ? const Center(child: Text('CtxApp')) : ListView(children: tiles),
    );
  }
}
