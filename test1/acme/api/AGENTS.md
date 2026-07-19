# AGENTS.md — Acme (api)

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
- All secrets via environment variables / configuration, never literals.
- Leave `// ctx:anchor:*` markers intact — the ctx0 CLI edits them.
