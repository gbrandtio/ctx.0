import 'package:flutter/material.dart';

/// Standard form field — underlined, prefix icon in the primary color
/// (docs/UI_UX_GUIDELINES.md §4A). Styling comes entirely from the global
/// InputDecorationTheme.
class AppTextField extends StatelessWidget {
  const AppTextField({
    super.key,
    required this.label,
    this.controller,
    this.prefixIcon,
    this.obscureText = false,
    this.keyboardType,
    this.textInputAction,
    this.validator,
    this.autofillHints,
    this.onSubmitted,
    this.enabled = true,
  });

  final String label;
  final TextEditingController? controller;
  final IconData? prefixIcon;
  final bool obscureText;
  final TextInputType? keyboardType;
  final TextInputAction? textInputAction;
  final String? Function(String?)? validator;
  final Iterable<String>? autofillHints;
  final ValueChanged<String>? onSubmitted;
  final bool enabled;

  @override
  Widget build(BuildContext context) {
    return TextFormField(
      controller: controller,
      obscureText: obscureText,
      keyboardType: keyboardType,
      textInputAction: textInputAction,
      validator: validator,
      autofillHints: autofillHints,
      onFieldSubmitted: onSubmitted,
      enabled: enabled,
      decoration: InputDecoration(
        labelText: label,
        prefixIcon: prefixIcon == null ? null : Icon(prefixIcon),
      ),
    );
  }
}
