import 'package:flutter/material.dart';

// ctx:gen:imports

/// Main navigation shell for CtxApp: a side navigation rail (tablet / desktop /
/// wide layouts) that switches between the selected feature tabs. Generated from
/// the workspace's navigation config — edit the choice with the ctx.0 tooling, or
/// this file directly to customise the shell.
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

  final List<NavigationRailDestination> _destinations =
      const <NavigationRailDestination>[
        // ctx:gen:destinations
      ];

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Row(
        children: [
          NavigationRail(
            selectedIndex: _index,
            onDestinationSelected: (i) => setState(() => _index = i),
            labelType: NavigationRailLabelType.all,
            destinations: _destinations,
          ),
          const VerticalDivider(width: 1),
          Expanded(child: _pages[_index]),
        ],
      ),
    );
  }
}
