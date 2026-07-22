# Architecture

Three documents, covering the generator and the two applications it produces.

- [**The ctx.0 generator**](generator.md). How the layering engine works: features as
  folders, anchors in shared files, the order layers are applied in, the parts of a
  workspace that are assembled rather than copied, and the packages and their contract.

- [**Generated mobile app**](mobile-app.md). The Flutter application in `app/`: startup and
  the app-level anchors, the always-on session layer that owns credentials, sign-in status
  and locale, Bloc state and repositories, the composition root, the generated navigation
  shell and theme, the two paths it uses to reach the API, and a worked example of a
  workspace generated with `profile`.

- [**Generated API**](api.md). The .NET application in `api/`: the four projects and their
  dependency direction, composition split across the host's `Configuration/` files, minimal API endpoint
  groups, EF Core with envelope encryption and row-level security, and how features serve
  each other through `Application` interfaces.

Related: the [UI/UX guidelines](../ui/README.md) bind the mobile templates, and
[`protocol/wire-protocol.md`](../../protocol/wire-protocol.md) specifies the app-to-API
protocol both sides implement.
