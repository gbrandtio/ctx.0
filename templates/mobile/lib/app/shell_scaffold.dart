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
    // ctx:nav_bottom:begin
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
    // ctx:nav_bottom:end

    // ctx:nav_rail:begin
    // ctx:off return Scaffold(
    // ctx:off   body: Row(
    // ctx:off     children: [
    // ctx:off       NavigationRail(
    // ctx:off         selectedIndex: shell.currentIndex,
    // ctx:off         onDestinationSelected: (index) => shell.goBranch(
    // ctx:off           index,
    // ctx:off           initialLocation: index == shell.currentIndex,
    // ctx:off         ),
    // ctx:off         labelType: NavigationRailLabelType.all,
    // ctx:off         destinations: [
    // ctx:off           for (final item in items)
    // ctx:off             NavigationRailDestination(
    // ctx:off               icon: Icon(item.icon),
    // ctx:off               selectedIcon: item.selectedIcon == null
    // ctx:off                   ? null
    // ctx:off                   : Icon(item.selectedIcon),
    // ctx:off               label: Text(item.label(context)),
    // ctx:off             ),
    // ctx:off         ],
    // ctx:off       ),
    // ctx:off       Expanded(child: shell),
    // ctx:off     ],
    // ctx:off   ),
    // ctx:off );
    // ctx:nav_rail:end

    // ctx:nav_drawer:begin
    // ctx:off return Scaffold(
    // ctx:off   body: shell,
    // ctx:off   appBar: AppBar(title: Text(items[shell.currentIndex].label(context))),
    // ctx:off   drawer: NavigationDrawer(
    // ctx:off     selectedIndex: shell.currentIndex,
    // ctx:off     onDestinationSelected: (index) {
    // ctx:off       Navigator.pop(context);
    // ctx:off       shell.goBranch(
    // ctx:off         index,
    // ctx:off         initialLocation: index == shell.currentIndex,
    // ctx:off       );
    // ctx:off     },
    // ctx:off     children: [
    // ctx:off       const DrawerHeader(child: SizedBox()),
    // ctx:off       for (final item in items)
    // ctx:off         NavigationDrawerDestination(
    // ctx:off           icon: Icon(item.icon),
    // ctx:off           selectedIcon: item.selectedIcon == null
    // ctx:off               ? null
    // ctx:off               : Icon(item.selectedIcon),
    // ctx:off           label: Text(item.label(context)),
    // ctx:off         ),
    // ctx:off     ],
    // ctx:off   ),
    // ctx:off );
    // ctx:nav_drawer:end

    // ctx:nav_none:begin
    // ctx:off return Scaffold(body: shell);
    // ctx:nav_none:end

    // ctx:nav_bottom_notched:begin
    // ctx:off return Scaffold(
    // ctx:off   body: shell,
    // ctx:off   floatingActionButton: FloatingActionButton(
    // ctx:off     onPressed: () {},
    // ctx:off     child: const Icon(Icons.add),
    // ctx:off   ),
    // ctx:off   floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
    // ctx:off   bottomNavigationBar: BottomAppBar(
    // ctx:off     shape: const CircularNotchedRectangle(),
    // ctx:off     child: Row(
    // ctx:off       mainAxisAlignment: MainAxisAlignment.spaceAround,
    // ctx:off       children: [
    // ctx:off         for (var i = 0; i < items.length; i++)
    // ctx:off           IconButton(
    // ctx:off             icon: Icon(shell.currentIndex == i
    // ctx:off                 ? (items[i].selectedIcon ?? items[i].icon)
    // ctx:off                 : items[i].icon),
    // ctx:off             color: shell.currentIndex == i
    // ctx:off                 ? Theme.of(context).colorScheme.primary
    // ctx:off                 : null,
    // ctx:off             onPressed: () => shell.goBranch(
    // ctx:off               i,
    // ctx:off               initialLocation: i == shell.currentIndex,
    // ctx:off             ),
    // ctx:off           ),
    // ctx:off       ],
    // ctx:off     ),
    // ctx:off   ),
    // ctx:off );
    // ctx:nav_bottom_notched:end

    // ctx:nav_tabs:begin
    // ctx:off return DefaultTabController(
    // ctx:off   length: items.length,
    // ctx:off   initialIndex: shell.currentIndex,
    // ctx:off   child: Scaffold(
    // ctx:off     appBar: AppBar(
    // ctx:off       bottom: TabBar(
    // ctx:off         onTap: (index) => shell.goBranch(
    // ctx:off         index,
    // ctx:off         initialLocation: index == shell.currentIndex,
    // ctx:off         ),
    // ctx:off         tabs: [
    // ctx:off           for (final item in items)
    // ctx:off             Tab(
    // ctx:off               icon: Icon(item.icon),
    // ctx:off               text: item.label(context),
    // ctx:off             ),
    // ctx:off         ],
    // ctx:off       ),
    // ctx:off     ),
    // ctx:off     body: shell,
    // ctx:off   ),
    // ctx:off );
    // ctx:nav_tabs:end
  }
}
