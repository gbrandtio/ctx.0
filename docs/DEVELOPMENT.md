# Developing ctx.0

This guide is for people working **on** ctx.0 itself — the composition engine and
the CLI — not for people using a generated workspace. It covers how to run the
tooling in development mode, how to build and install it, and how to test changes.

For **how the system is structured and why**, read
[architecture/README.md](architecture/README.md) and the per-subsystem documents it
links; the decisions behind that structure are in [adr/](adr/). This guide assumes
that context rather than repeating it.

## Prerequisites

- **Node.js ≥ 20** (`engines.node` in every `package.json`; CI and the maintainers
  run current LTS or newer).
- **npm** with workspaces support (npm 8+). The repo is a plain npm workspace
  monorepo — no pnpm/yarn.
- **Flutter** and the **.NET SDK** are only needed if you want to generate a *full*
  workspace (`create workspace` without `--no-platforms`) and actually run the app.
  You do **not** need them to develop, build, or test the engine and CLI — see
  [Working without Flutter / .NET](#working-without-flutter--net).

Install once from the repo root:

```bash
npm install
```

This installs and links both workspaces (`@ctx0/core` and `ctx0`).

## Where things are

| Path | What it is | Document |
|---|---|---|
| `packages/core` | `@ctx0/core` — the CLI-free composition engine | [architecture/core.md](architecture/core.md) |
| `packages/engine-server` | `@ctx0/engine-server` — the contract + JSON-RPC/MCP stdio server | [architecture/engine-server.md](architecture/engine-server.md) |
| `packages/cli` | `ctx0` — the command-line frontend | [architecture/cli.md](architecture/cli.md) |
| `templates/` | the trees the engine composes from | [architecture/templates.md](architecture/templates.md) |
| `protocol/` | wire-protocol spec + golden vectors | [architecture/protocol.md](architecture/protocol.md) |

Two facts govern almost every change:

- **All scaffolding logic lives in `@ctx0/core`.** The CLI and the engine server
  are thin. The CLI does not import the engine — it spawns `ctx0-engine` and
  talks to it over the contract in
  `packages/engine-server/src/contract.ts`. New CLI capability means a new call,
  not an import. ([ADR-0001](adr/0001-cli-never-imports-core.md),
  [ADR-0002](adr/0002-engine-over-jsonrpc-mcp-stdio.md))
- **Build order is `core` → `engine-server` → `cli`**, since the CLI compiles
  against the contract's generated types. The root `build` script does this.

To exercise the engine on its own:

```bash
npm run build
node packages/engine-server/dist/index.js   # speaks JSON-RPC on stdin/stdout
```

## Build

TypeScript compiles to `dist/` in each package. The root `build` script builds
`@ctx0/core` first (the CLI depends on it), then the CLI:

```bash
npm run build            # from repo root: builds core, then cli
```

Per-package, from inside `packages/core` or `packages/cli`:

```bash
npm run build            # tsc -p tsconfig.json
npm run dev              # core: tsc -w (watch, recompile on change)
```

`dist/` is git-ignored; it is a build artifact.

## Running the CLI in development

There are two ways to run the CLI while developing.

### 1. `tsx` — run TypeScript source directly (fastest inner loop)

No build step; runs `src/index.ts` as-is. Best while iterating on CLI code.

```bash
cd packages/cli
npm run dev -- status
npm run dev -- create workspace Demo --org com.demo --no-platforms
npm run dev -- keygen
```

Everything after `--` is passed through to the CLI as argv.

### 2. Built output — exercise what actually ships

Build first, then run the compiled entrypoint. This is what an installed `ctx0`
binary runs, so use it to sanity-check the real artifact:

```bash
npm run build
node packages/cli/dist/index.js status          # from repo root
# or via the root convenience script:
npm run ctx0 -- status
```

### Command reference (for manual testing)

| Command | What it does |
|---|---|
| `ctx0 create workspace <name>` | Scaffold a full workspace (Flutter app + .NET API). |
| `ctx0 create workspace <name> --no-platforms` | Generate only the ctx.0 source overlay — **skips `flutter create`**. |
| `ctx0 status` | Outside a workspace: list the feature catalog. Inside one: show enabled features. |
| `ctx0 keygen` | Print a fresh set of server secrets (ALE key pair, JWT signing key, KEK, blind-index key) as env vars. |

Useful `create` options: `-o, --org <reverse-dns>`, `-d, --dir <parent>`,
`-f, --features <ids...>` (default: `ping auth notes`).

## Installing `ctx0` onto your PATH

The CLI package declares a `bin` (`ctx0 → dist/index.js`), so once built it can be
put on your PATH as a real `ctx0` command. **Build first** — the bin points at
`dist/`, so an unbuilt package links a non-existent entrypoint.

### Dev install: `npm link` (recommended)

Symlinks the workspace into your global bin. Because it's a symlink, rebuilds are
picked up immediately — no re-link needed after `npm run build`.

```bash
npm run build                 # from repo root — builds core, then cli
cd packages/cli
npm link                      # creates the global `ctx0` symlink

ctx0 --version
ctx0 status
```

Iterate loop afterward: edit source → `npm run build` → run `ctx0` again. (For the
tightest loop while editing CLI code, `npm run dev -- <args>` via `tsx` is still
faster since it skips the build; use the linked binary to test the shipping
artifact.)

To remove the link:

```bash
cd packages/cli && npm unlink -g ctx0     # or: npm rm -g ctx0
```

> The bin resolves `@ctx0/core` through the workspace symlink, so `@ctx0/core`
> must be built too. The root `npm run build` handles that ordering for you.

### One-off global install from source

Installs a copy (not a symlink) — you must reinstall after every change, so this is
for smoke-testing an install rather than for iterating:

```bash
npm run build
npm install -g ./packages/cli
ctx0 --version
npm rm -g ctx0                # uninstall
```

### No install: run the built entrypoint directly

If you don't want anything on your PATH:

```bash
npm run build
node packages/cli/dist/index.js status
# or the root convenience script:
npm run ctx0 -- status
```

## Testing

Tests are [Vitest](https://vitest.dev). The engine suite lives in `@ctx0/core`
and the boundary suite in `@ctx0/engine-server`; the CLI has no unit tests and
passes with `--passWithNoTests`.

```bash
npm test                 # repo root: runs tests across all workspaces
```

Per-package, from inside `packages/core`:

```bash
npm test                 # vitest run (one-shot)
npm run test:watch       # vitest watch mode
```

Run a single file or filter by name:

```bash
cd packages/core
npx vitest run test/compose.test.ts
npx vitest run -t "rejects unknown features"
```

### What the tests cover / how they work

- `test/substitute.test.ts` — variable resolution (`resolveVars`: app name → slug,
  bundle id, org).
- `test/agents.test.ts` — composable `AGENTS.md` assembly.
- `test/compose.test.ts` — the end-to-end scaffolder: it composes a workspace into
  an **OS temp dir** (`fs.mkdtemp`) and asserts on the generated tree, then cleans
  up in `afterEach`. These tests call `createWorkspace` **without**
  `scaffoldPlatforms`, so they never invoke `flutter` — they run anywhere.
- `packages/engine-server/test/tools.test.ts` — every call in the contract:
  that each one is implemented, that required arguments are rejected, and what
  each returns.
- `packages/engine-server/test/stdio.test.ts` — spawns the built engine and
  drives it with a hand-rolled JSON-RPC client (no SDK), which is what proves the
  contract is usable from another language. That package's `test` script builds
  first, because this test runs `dist/`.

When you add engine behavior, add a compose-level assertion here; when you change
the contract, cover the new call in `tools.test.ts`.

### Manual end-to-end check

To eyeball a generated workspace without touching Flutter/.NET, generate the
overlay into a scratch dir:

```bash
npm run build
cd /tmp && node /path/to/ctx.0/packages/cli/dist/index.js \
  create workspace Demo --org com.demo --no-platforms
# inspect ./demo, then:
cd demo && node /path/to/ctx.0/packages/cli/dist/index.js status   # shows enabled features
```

`create` prints the env vars the API needs and the manual next steps; verify those
match your change. Generated/scratch workspaces named `/.ctx-tmp/` or
`/tmp-workspace/` are git-ignored, but prefer generating **outside** the repo tree.

## Type-checking / lint

There is no ESLint config; `lint` is a type-check (`tsc --noEmit`). Note the root
`lint` script builds core first so the CLI can resolve `@ctx0/core` types:

```bash
npm run lint             # repo root
# per package:
cd packages/core && npm run lint
```

## Working without Flutter / .NET

The engine, CLI, build, and full test suite run with **only Node installed**. Two
things make this possible:

1. `--no-platforms` on `create workspace` skips the `flutter create` shell-out and
   emits just the ctx.0 source overlay.
2. The compose tests always run in the no-platforms mode.

You only need the Flutter and .NET toolchains when you want to *build and run* a
generated app — i.e. for template/runtime work, not for engine or CLI work.

## Working on templates

Templates are read at runtime, so **editing one is often enough to change the
output** — no engine change and, for `tsx`-based runs, no rebuild. `ctx0 status`
is the quickest way to confirm a new feature is discovered with the right sides
and summary.

How overlays, manifests, wiring anchors and translation fragments work is in
[architecture/templates.md](architecture/templates.md), with the merge and
composition rules in [architecture/core.md](architecture/core.md). Three things
bite most often while editing:

- **Keep both template layouts resolvable.** `packages/core/src/paths.ts` locates
  the trees at the repo root (monorepo) or next to `dist/` (published package). If
  you move directories, keep both working.
- **Translation keys are namespaced by feature** (`profileTitle`,
  `media.tooLarge`) — two layers defining the same key is a hard error — and
  English must always be complete.
- **Sort with `sortUtf8`** (`packages/core/src/order.ts`), never a bare `.sort()`.
  The order determines overlay hashes and the manifest's file lists.
  ([ADR-0006](adr/0006-deterministic-composition-ordering.md))

## Conventions

- Keep all scaffolding logic in `@ctx0/core`; the CLI and engine server stay thin.
- The CLI reaches the engine only through the contract. New CLI capability means
  a new call in `contract.ts`, not an import of `@ctx0/core`.
- No TODO markers or "will do later" language in committed code or docs — work is
  either done or not on the branch.
- Match the surrounding code's style, naming, and comment density.
