# AGENTS.md — CtxApp (api)

.NET REST API in **Clean Architecture**. Dependencies point inward only:

```
Api  ->  Infrastructure  ->  Application  ->  Domain
                    \-------------------------/
```

- **Domain** (`src/Domain`): entities, value objects. No framework/EF references.
- **Application** (`src/Application`): use-cases + abstractions (interfaces).
- **Infrastructure** (`src/Infrastructure`): EF Core (code-first, PostgreSQL), repositories, the security implementations.
- **Api** (`src/Api`): thin ASP.NET host; `Program.cs` wires everything.

Rules:
- EF Core is **code-first**; add a migration for every schema change.
- The security plane is vendored under `src/Security/` — `AddCtxSecurity` / `UseCtxSecurity` in `Program.cs`. Do not weaken it.
- Localization is always-on under `src/Api/Localization/` — `AddCtxLocalization` / `UseCtxLocalization` in `Program.cs`. The request culture comes from `Accept-Language`; endpoints take `IStringLocalizer<Messages>` and read feature-namespaced keys (`auth.invalidCredentials`). `SupportedCultures.g.cs` and the `Resources/Localization/Messages*.resx` files are generated from the chosen languages — regenerate the workspace to change them. Keep validation/error text out of the domain; resolve strings at the edge.
- All secrets via environment variables / configuration, never literals.
- Leave `// ctx:anchor:*` markers intact — the ctx0 CLI edits them.
