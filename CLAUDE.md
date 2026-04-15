# CLAUDE.md — limentinus (universal node agent)

> **Project**: Limentinus — component of [Limen](https://github.com/getlimen/limen)
> **Role**: Universal agent. Installed on every node Limen manages. Roles (`control`, `docker`, `proxy`) determine which capabilities it enables. Maintains a WireGuard tunnel to Forculus and a persistent WebSocket to Limen.

**For full project context, read [`limen/docs/HANDOFF.md`](https://github.com/getlimen/limen/blob/main/docs/HANDOFF.md) and [`limen/docs/CONVENTIONS.md`](https://github.com/getlimen/limen/blob/main/docs/CONVENTIONS.md).**

## Workflow rules (enforced, apply to every repo in `getlimen`)

- **Never work on `main`.** Create issue (labeled) → branch `<type>/<issue>_<PascalCaseName>` → PR (labeled) with `Closes #<issue>` → squash-merge + delete branch.
- **Use CLI generators whenever one exists.** `dotnet new`, `dotnet ef`, `gh issue create`, `gh pr create`, etc. If you don't know the command, search online before hand-writing boilerplate.
- **No AI / Claude attribution** in commits or PRs.

## Etymology
*Limentinus* — minor Roman deity, the **guardian-spirit of the threshold** (*limen*). Named directly after *limen* itself. Limentinus watches over each doorway — just as our agent watches over each managed host.

## Tech Stack

- .NET 10 / ASP.NET Core Worker Service with **NativeAOT**
- **Docker.DotNet** for container management (when `docker` role active)
- **wireguard-go** userspace binary + UAPI unix socket (cross-platform, no kernel module needed on the agent side)
- **JSON over WebSocket** to Limen (auto-reconnect, 1s→60s backoff)
- Local persistence: just the node identity file at `/var/lib/limentinus/identity.json` (mode 0600)

## Role flags

Set via `LIMEN_ROLES` env (comma-separated):

| Role | Enables |
|------|---------|
| `control` | Expects Limen to also run on this host (loopback connection) |
| `docker` | Docker socket access, runs the DeployPipeline for service deployments |
| `proxy` | Brings up a local Ostiarius container; public TLS terminates here |

Freely combinable.

## Deploy pipeline (`docker` role)

Explicit stages — NOT a god class (Coolify anti-pattern):

1. `PullImageStage` — `docker pull` with progress streaming
2. `CaptureOldStage` — remember previous container for rollback
3. `StartNewStage` — run new container with suffixed name
4. `HealthCheckStage` — poll healthcheck with backoff
5. `FinalizeStage` — stop old, rename new, remove old
6. `RollbackStage` — if any stage fails: restart old, mark failed

Each stage is a small class implementing `IDeployStage`. Composable, testable, debuggable.

## Enrollment flow

1. First start: reads `LIMEN_PROVISIONING_KEY` env
2. POSTs to Limen with key + hostname + roles + platform
3. Receives `{ agentId, permanentSecret, wireguardConfig }` in response
4. Writes identity to `/var/lib/limentinus/identity.json` (0600)
5. Brings up local wg0 via embedded wireguard-go (UAPI socket)
6. Opens permanent WebSocket to Limen using `Authorization: Bearer {agentId}:{secret}`
7. If `proxy` role: pulls + runs Ostiarius container
8. Listens for commands; heartbeat every 30s

Restart: re-read identity file, skip enrollment, reconnect tunnel + WS.

## Clean architecture

Same strict rules as `limen`. See [`limen/CLAUDE.md`](https://github.com/getlimen/limen/blob/main/CLAUDE.md).

## What Limentinus does NOT do

- Store service/route definitions (Limen does)
- Make scheduling decisions (Limen does)
- Talk to other agents directly (all traffic via Limen/Forculus)
- Contain persistent state beyond its own identity

## Conventions

Same as `limen`: English-only, Apache 2.0, conventional commits, **no AI attribution in commits**.
