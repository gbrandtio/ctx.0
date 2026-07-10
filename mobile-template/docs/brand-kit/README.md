# Brand Kit

**All branding material for the app lives in this folder** — nowhere else. This includes:

- App logo (SVG preferred; provide a raster fallback only if a consumer requires it, e.g., map marker rendering)
- Custom icons for metric cards (SVG)
- Any partner logos required by branding guidelines

## Rules

1. **Agents**: when a task produces or requires branding assets, place them here and reference them by relative path from the docs (e.g., `docs/brand-kit/logo.svg`). Never scatter brand assets in `lib/assets` ad hoc — the build copies from a single, documented asset list.
2. **SVG first**: raster images don't scale and bloat the app. See `docs/UI_UX_GUIDELINES.md` §4D.
3. **Colors and fonts** are not stored here — they are code, defined in `lib/core/theme/app_colors.dart` / `app_theme.dart` per `docs/UI_UX_GUIDELINES.md`.

## Bundled assets

| File | Purpose |
|---|---|
| `google_logo.svg` | Official Google "G" logo for the Google Sign-In button (required by [Google's branding guidelines](https://developers.google.com/identity/branding-guidelines)). Do not recolor or modify. |
