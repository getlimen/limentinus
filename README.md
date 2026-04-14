# Limentinus

> *Roman deity — the guardian-spirit of thresholds.* Named directly after *limen* itself.

Limentinus is the **universal node agent** for [Limen](https://github.com/getlimen/limen). Install it on any host you want Limen to manage. Role flags (`control` / `docker` / `proxy`) determine which capabilities the agent enables.

## Install

```bash
# compose.yml snippet (admin UI generates this with a provisioning key)
services:
  limentinus:
    image: ghcr.io/getlimen/limentinus:latest
    restart: unless-stopped
    environment:
      LIMEN_CENTRAL_URL: wss://limen.example.com
      LIMEN_PROVISIONING_KEY: <one-shot-key-from-UI>
      LIMEN_ROLES: docker            # or docker,proxy / control,docker,proxy / etc.
    volumes:
      - limentinus_state:/var/lib/limentinus
      - /var/run/docker.sock:/var/run/docker.sock  # only if docker role
volumes:
  limentinus_state:
```

Run:
```bash
docker compose up -d
```

The agent enrolls using the provisioning key, receives a permanent identity, establishes a WireGuard tunnel back to Limen's hub, and starts accepting commands.

## Roles

| Flag | Enables |
|------|---------|
| `control` | This is the host where Limen itself runs (expects loopback connection) |
| `docker` | Docker socket access; this node runs user services |
| `proxy` | Local Ostiarius container brought up; public TLS terminates here |

Freely combinable.

## What Limentinus does

- Enrolls via one-shot provisioning key (gets permanent credentials)
- Maintains WireGuard tunnel to Forculus
- Keeps persistent JSON/WebSocket to Limen (auto-reconnect, 1s→60s backoff)
- Executes deploys via explicit pipeline stages (pull → start new → health check → finalize → rollback on fail)
- Streams deployment logs back to Limen in real time
- If `proxy` role: supervises local Ostiarius container

## Tech stack

.NET 10 / NativeAOT • Docker.DotNet • wireguard-go (userspace) • System.Net.WebSockets • Serilog

## Status

In active development. See [`limen/docs/superpowers/plans/2026-04-14-plan-02-agent-control-channel.md`](https://github.com/getlimen/limen/blob/main/docs/superpowers/plans/2026-04-14-plan-02-agent-control-channel.md).

## License

[Apache 2.0](LICENSE)
