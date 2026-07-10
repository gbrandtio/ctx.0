# Flutter Performance Optimization Knowledge Base

**Target Audience:** Automated Code Reviewers, Performance Auditors
**Context:** Optimization of Flutter applications across Mobile and Web platforms.
**Goal:** Achieve <16ms frame rendering time (60fps) and minimize startup latency.

---

## 1. General Architecture & Build Cycle

### Rule 1.1: Prefer Separate Widgets over Helper Methods
**Context:** Breaking down large `build()` methods.
* **Anti-Pattern:** Using helper methods (e.g., `_buildHeader()`) to return widget trees.
* **Best Practice:** Create a separate `StatelessWidget` or `StatefulWidget`.
* **Reasoning:**
    * **Rebuild Scope:** When `setState` is called in the parent, helper methods rebuild *every time*, whereas separate `const` Widgets can be skipped by the framework.
    * **Context Safety:** Helper methods share the parent's `BuildContext`, which can lead to stale context bugs in async operations.
    * **Optimization:** Separate widgets allow Flutter to cache elements and render objects more effectively.

### Rule 1.2: Maximize `const` Constructors
* **Action:** Always use `const` for widgets that do not change.
* **Impact:** Allows the framework to "short-circuit" the build phase for that subtree, reusing the existing Element and RenderObject.
* **Detection:** Look for `Container(...)`, `Text(...)`, or `Icon(...)` calls that depend only on static data but lack the `const` keyword.

### Rule 1.3: Control `build()` Cost
* **Threshold:** `build()` methods should not perform expensive synchronous work (e.g., I/O, heavy calculation).
* **Heuristic:** If a method takes >5ms, move it out of `build()`.
* **Technique:** Move expensive calculations to `initState` or background isolates.

---

## 2. Widget-Specific Optimizations

### 2.1 StatefulWidgets
* **State Location:** "Push state to the leaves."
    * *Example:* If a clock ticks, wrap only the clock text in a StatefulWidget, not the entire page.
* **Subtree Caching:** If a subtree is static but resides inside a rebuilding parent:
    * Assign the subtree to a `final` variable.
    * Pass it as a `child` parameter to the rebuilding widget.

### 2.2 AnimatedBuilder & Animations
* **Optimization Target:** Prevent unrelated widgets from rebuilding during animation ticks.
* **Pattern:**
    ```dart
    // BAD: Rebuilds 'hugeChild' every frame
    AnimatedBuilder(
      animation: controller,
      builder: (ctx, child) => Transform.rotate(angle: controller.value, child: hugeChild),
    );

    // GOOD: Builds 'hugeChild' once
    AnimatedBuilder(
      animation: controller,
      child: hugeChild, // Pass static subtree here
      builder: (ctx, child) => Transform.rotate(angle: controller.value, child: child),
    );
    ```

### 2.3 Opacity & Clipping
* **Cost:** `Opacity` widget and clipping (e.g., `ClipRRect`) often trigger `saveLayer()`, which requires an offscreen buffer (expensive).
* **Alternatives:**
    * **Static Opacity:** Use semitransparent colors (e.g., `Colors.red.withValues(alpha: 0.5)`. Note that `withOpacity` is deprecated) instead of wrapping a solid color Container in `Opacity`.
    * **Images:** Use `FadeInImage` or specific image opacity parameters.
    * **Animation:** Use `AnimatedOpacity` or `FadeTransition` instead of wrapping `Opacity` in an animation loop.
* **Detection:** Check for `Opacity` widgets wrapping simple primitives or acting as animation targets.

### 2.4 ListView & Grids
* **Item Extent:**
    * If items have a fixed size, use `itemExtent` (ListView) or `mainAxisExtent` (GridView).
    * *Benefit:* Allows the scroll machinery to calculate scroll position without building every child to measure it.
* **Prototype Item:** If items are variable but likely the same size as a prototype, use `prototypeItem`.
* **Constructors:**
    * Use `ListView.builder` for lists with unbounded or large (>20) item counts.
    * Use `ListView` constructor (explicit list) *only* for very small, static lists.
* **Lifecycle:**
    * Use `AutomaticKeepAliveClientMixin` sparingly; keeping too many items alive consumes memory.

---

## 3. Web-Specific Optimizations (Loading Speed)

### 3.1 Rendering Engine
* **Scenario:** Initial load time is critical.
* **Trade-off:**
    * **CanvasKit:** Good for fidelity/performance but large download (~1.5MB).
    * **Wasm (Experimental):** Smaller, faster startup, closer to native performance.
* **Action:** Evaluate `dart2wasm` if utilizing modern Flutter versions.

### 3.2 Deferred Loading
* **Strategy:** Split the compiled Javascript/Wasm bundle.
* **Implementation:** Use `deferred as` imports for libraries not needed immediately (e.g., Settings page, obscure features).
    ```dart
    import 'package:my_heavy_lib/lib.dart' deferred as heavy;
    // ...
    await heavy.loadLibrary();
    heavy.doWork();
    ```

### 3.3 Asset Optimization
* **Image Formats:** Prefer WebP or AVIF over PNG/JPG for significantly smaller file sizes with comparable quality.
* **Preloading:** Use `<link rel="preload">` in `index.html` for critical assets (fonts, logo) to unblock the first paint.

---

## 4. Performance Debugging Heuristics

When analyzing a performance trace or code snippet, apply these checks:

| Symptom | Probable Cause | Investigation Tool |
| :--- | :--- | :--- |
| **Jank (dropped frames)** | Excessive work in `build()` or `layout` | DevTools CPU Profiler |
| **High GPU usage** | `saveLayer` calls (Opacity, Clipping, Shadows) | DevTools "Checkerboard Offscreen Layers" |
| **Scroll stutter** | Lack of `itemExtent` or expensive widget builds in list items | DevTools Track Widget Rebuilds |
| **Slow Web Startup** | Large main.dart.js, no deferred loading, massive assets | Network Tab (Browser) |
| **Memory Spike** | Image caching issues or large lists with KeepAlive | DevTools Memory View |

---

## 5. Resource Reference
* **Docs:** [Flutter Performance Best Practices](https://docs.flutter.dev/perf/best-practices)
* **Video:** [Widgets vs Helper Methods](https://www.youtube.com/watch?v=IOyq-eTRhvo)
* **API:** [StatefulWidget Performance](https://api.flutter.dev/flutter/widgets/StatefulWidget-class.html#performance-considerations)
* **API:** [AnimatedBuilder Optimization](https://api.flutter.dev/flutter/widgets/AnimatedBuilder-class.html#performance-optimizations)