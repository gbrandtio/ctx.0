import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'feature_module.dart';

/// Bottom-navigation scaffold built from the registered NavItems
/// (docs/APP_SHELL.md). One StatefulShellBranch per nav module keeps each
/// tab's navigation stack alive across switches.
class ShellScaffold extends StatelessWidget {
  const ShellScaffold({
    super.key,
    required this.shell,
    required this.items,
  });

  final StatefulNavigationShell shell;
  final List<NavItem> items;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: shell,
      bottomNavigationBar: NavigationBar(
        selectedIndex: shell.currentIndex,
        onDestinationSelected: (index) => shell.goBranch(
          index,
          // Re-tapping the active tab pops to its root.
          initialLocation: index == shell.currentIndex,
        ),
        destinations: [
          for (final item in items)
            NavigationDestination(
              icon: Icon(item.icon),
              selectedIcon:
                  item.selectedIcon == null ? null : Icon(item.selectedIcon),
              label: item.label(context),
            ),
        ],
      ),
    );
  }
}
