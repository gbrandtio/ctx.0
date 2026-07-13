# ctx0_cli

The ctx.0 scaffolder. Generates production-grade Flutter (+ .NET, coming)
applications with a permanent, non-removable security plane
(`ctx0_mobile_security` / `Ctx0.Security`), plug-n-play feature modules, and
LLM-facing build instructions materialized into every generated repo.

```bash
dart pub global activate ctx0_cli

ctx0 create app acme --org com.acme --with maps_google
ctx0 status | ctx0 enable <id> | ctx0 disable <id> | ctx0 doctor
ctx0 docs sync
```

Inside a generated repo, `AGENTS.md` routes your coding agent to the
binding docs; `docs/packages/` describes the installed security packages;
`ctx0 doctor` verifies injection marker consistency and that the security
plane is intact (present, not overridden, not vendored).
