import 'package:flutter/material.dart';

/// The single source of brand colors (docs/UI_UX_GUIDELINES.md §3).
/// Replace the placeholder values with your brand palette; never add
/// colors anywhere else in the app.
abstract final class AppColors {
  // ---- Brand (placeholders — replace) ----
  static const Color primary = Color(0xFF3F51B5);
  static const Color primaryDark = Color(0xFF7986CB);

  // ---- Light mode ----
  static const Color backgroundLight = Color(0xFFFFFFFF);
  static const Color surfaceLight = Color(0xFFFFFFFF);
  static const Color textPrimaryLight = Color(0xFF1F1F1F);
  static const Color textSecondaryLight = Color(0xFF757575);

  // ---- Dark mode ----
  static const Color backgroundDark = Color(0xFF121212);
  static const Color surfaceDark = Color(0xFF1E1E1E);
  static const Color textPrimaryDark = Color(0xFFFFFFFF);
  static const Color textSecondaryDark = Color(0xFFBDBDBD);

  // ---- Semantic ----
  static const Color error = Color(0xFFF44336);
}
