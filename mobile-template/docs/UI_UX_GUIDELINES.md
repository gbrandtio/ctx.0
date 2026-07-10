# UI/UX Guidelines

This document outlines the design system and visual identity rules for the application. Adherence to these guidelines ensures a consistent, modern, and accessible user experience across all platforms. Concrete brand values (colors, fonts, logos) are project-specific: define them once in the theme files below and store the source assets in `docs/brand-kit/` (see its README).

## 1. Core Principles
- **Modern Minimalism**: Clean layouts with ample white space and functional elements.
- **Visual Consistency**: Every screen must inherit from the global `AppTheme`. Never hardcode colors, radii, or text styles inside feature widgets.
- **Accessibility**: High contrast for text and interactive elements in both Light and Dark modes (WCAG AA minimum: 4.5:1 for body text).

## 2. Typography
- **Primary Font**: Your brand font (declared in `docs/brand-kit/`), managed via the `google_fonts` package (or bundled font assets) in `AppTheme`.
- **Locale Strategy**: If the primary font renders non-Latin scripts poorly, define a per-locale font override in `AppTheme` (e.g., a secondary font for Greek/Cyrillic), with `Roboto, sans-serif` as the final fallback.
- **Usage**:
    - **Headlines**: Bold.
    - **Body**: Regular/Medium.
    - **Input Labels**: Medium, 14pt.

## 3. Color Palette
Colors are centralized in `lib/core/theme/app_colors.dart`. Replace the placeholder values with your brand palette; do not add colors anywhere else.

| Token | Light Mode | Dark Mode |
| :--- | :--- | :--- |
| **Primary** | Your primary brand color | Your primary brand color |
| **Background** | White `#FFFFFF` | Near-black `#121212` |
| **Surface/Card** | White `#FFFFFF` | Dark Gray `#1E1E1E` |
| **Text Primary** | Dark Gray `#1F1F1F` | White `#FFFFFF` |
| **Text Secondary** | Gray `#757575` | Light Gray `#BDBDBD` |
| **Error** | Red `#F44336` | Red `#F44336` |

## 4. Components & Styling

### A. Forms & Inputs
- **Style**: Underlined (Minimalist).
- **Implementation**: Global `InputDecorationTheme` in `AppTheme`.
- **Standards**:
    - Avoid `OutlineInputBorder` unless explicitly required for a specific non-form component.
    - Focus color must always be the **Primary** color.
    - Error color must always be the **Error** color.
    - Prefix icons should use the Primary color to maintain brand identity.

### B. Lists & Cards
- **Style**: Elevated Cards.
- **Implementation**: Global `CardThemeData` in `AppTheme`.
- **Standards**:
    - **Elevation**: 4.0
    - **Border Radius**: 12.0
    - **Margin**: `symmetric(horizontal: 16, vertical: 8)`
    - Content inside cards should generally use `ListTile` for standardized layout.

### C. Buttons
- **Primary Action**: `ElevatedButton`.
    - Rounded corners (Radius 12).
    - Full-width (`double.infinity`) with a minimum height of 50.
    - Background: Primary color.
    - Text: White (Light Mode) / Black (Dark Mode). Adjust for contrast against your primary color.
- **Secondary Action**: `TextButton` or `OutlinedButton`.

### D. Icons & Assets
- **Standard Icons**: Prefer Material Symbols (Outlined) or Cupertino Icons for generic UI actions.
- **Custom Brand Icons**: Use Scalable Vector Graphics (SVG) for metric cards and brand-specific elements.
    - **Source**: SVG source files live in `docs/brand-kit/`.
    - **Implementation**: Define SVGs as string constants in `lib/core/widgets/app_icons.dart`.
    - **Rendering**: Use the `AppIcon` wrapper to ensure consistent sizing and theme-aware coloring (Primary color).
    - **Avoid Raster**: Do not use PNG/JPG for simple icons; they do not scale well and increase app size.

## 5. Layout & Spacing
- **Screen Padding**: Standard 24.0 or 16.0 for content containers.
- **Vertical Spacing**: Use `SizedBox` with increments of 8 (8, 16, 24, 32, 48).
- **SafeArea**: All screens must respect `SafeArea` to avoid notch/system bar collisions.

## 6. Theme Modes
The application supports dynamic switching between **Light** and **Dark** modes.
- **Light Mode**: High clarity, white-based backgrounds.
- **Dark Mode**: OLED-friendly, dark gray backgrounds with vibrant primary accents.

---
*For technical implementation details, refer to `lib/core/theme/app_theme.dart`.*
