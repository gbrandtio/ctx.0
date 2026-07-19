import 'package:flutter/material.dart';

// ctx:gen:imports

/// Main navigation shell for CtxApp: a single landing screen that lists each
/// feature as a tile pushing its screen. Generated from the workspace's
/// navigation config — edit the choice with the ctx.0 tooling, or this file
/// directly to customise the shell.
class CtxShell extends StatelessWidget {
  const CtxShell({super.key});

  @override
  Widget build(BuildContext context) {
    final tiles = <Widget>[
      // ctx:gen:tiles
    ];
    return Scaffold(
      appBar: AppBar(title: const Text('CtxApp')),
      body: ListView(children: tiles),
    );
  }
}
