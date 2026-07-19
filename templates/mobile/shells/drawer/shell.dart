import 'package:flutter/material.dart';

// ctx:gen:imports

/// Main navigation shell for CtxApp: a hamburger navigation drawer that switches
/// between the selected feature tabs. Generated from the workspace's navigation
/// config — edit the choice with the ctx.0 tooling, or this file directly to
/// customise the shell.
class CtxShell extends StatefulWidget {
  const CtxShell({super.key});

  @override
  State<CtxShell> createState() => _CtxShellState();
}

class _CtxShellState extends State<CtxShell> {
  int _index = 0;

  final List<Widget> _pages = <Widget>[
    // ctx:gen:pages
  ];

  final List<Widget> _destinations = const <Widget>[
    // ctx:gen:destinations
  ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('CtxApp')),
      drawer: NavigationDrawer(
        selectedIndex: _index,
        onDestinationSelected: (i) {
          setState(() => _index = i);
          Navigator.of(context).pop();
        },
        children: _destinations,
      ),
      body: _pages[_index],
    );
  }
}
