# ctx.0 documentation

ctx.0 is a security-first scaffolder: it composes a workspace (a Flutter app and a .NET
API that speak an encrypted, signed wire protocol) from template trees, and hands the
result to the user.

## Start here

| If you want to… | Read |
|---|---|
| understand how ctx.0 is put together | [architecture/README.md](architecture/README.md) |
| work on ctx.0 itself + build, test, run, install | [DEVELOPMENT.md](DEVELOPMENT.md) |
| know why it is shaped this way | [adr/README.md](adr/README.md) |

## Architecture

One document for the system, one per subsystem. Each follows the same structure — purpose,
boundaries, module map, key flows, data shapes, invariants, extension points — so they can
be read side by side.

| Document | Covers |
|---|---|
| [architecture/README.md](architecture/README.md) | The whole picture: context, package graph, process boundaries, the composition model, and a "where do I change X" routing table |
| [architecture/core.md](architecture/core.md) | `@ctx0/core` the composition engine which holds all scaffolding logic |
| [architecture/engine-server.md](architecture/engine-server.md) | `@ctx0/engine-server` the versioned contract and the JSON-RPC/MCP stdio server |
| [architecture/cli.md](architecture/cli.md) | `ctx0` the CLI frontend, a client of the contract |
| [architecture/templates.md](architecture/templates.md) | `templates/` base trees, feature overlays, navigation shells, the security plane, translation fragments |
| [architecture/protocol.md](architecture/protocol.md) | `protocol/` the app↔API wire protocol and its golden vectors |
| [architecture/generated-workspace.md](architecture/generated-workspace.md) | The architecture of what ctx.0 emits |
| [architecture/ci.md](architecture/ci.md) | `.github/workflows` commit health gate, code metrics |

## Decisions

[adr/](adr/) holds one file per architectural decision, with the context that forced it and
the costs it carries.

## Reading order

New to the codebase, front to back:

1. [architecture/README.md](architecture/README.md) the system, and where things live.
2. [architecture/cli.md](architecture/cli.md) how a command becomes engine calls.
3. [architecture/engine-server.md](architecture/engine-server.md) the contract those
   calls cross.
4. [architecture/core.md](architecture/core.md) what happens inside `workspace.create`.
5. [architecture/templates.md](architecture/templates.md) what it composes from.
6. [architecture/generated-workspace.md](architecture/generated-workspace.md) what comes
   out.
7. [DEVELOPMENT.md](DEVELOPMENT.md) build it and run it yourself.

Following steps 1–4 should let you trace `ctx0 create workspace` end to end without opening
a source file.
