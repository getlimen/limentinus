# Limentinus

> *Latin: "threshold guardian"* — minor Roman deity, guardian-spirit of the threshold (*limen*) itself.

**Limentinus** is the universal node agent for [Limen](https://github.com/getlimen/limen). Installed on every managed host, it handles enrollment, WireGuard tunnel setup, Docker deployments, and proxy orchestration.

## Quick start

```bash
docker compose up -d
```

Required environment variables:
- `LIMEN_CENTRAL_URL` — WebSocket URL to Limen (e.g., `ws://limen.example.com:8080`)
- `LIMEN_PROVISIONING_KEY` — one-time key from Limen admin UI
- `LIMEN_ROLES` — comma-separated: `docker`, `proxy`, or both

## Features

- **Auto-enrollment** — provisions identity, receives WireGuard config, connects tunnel
- **Docker deploy pipeline** — explicit stages: pull → capture old → start new → health check → finalize (or rollback)
- **WireGuard tunnel** — userspace `wg-quick` on the agent side
- **Persistent WebSocket** — auto-reconnecting control channel to Limen
- **Role-based** — `docker` role enables container management, `proxy` role brings up Ostiarius

## Tech stack

.NET 10 / ASP.NET Core Worker • Docker.DotNet • wireguard-go (userspace)

## Architecture

See the [Limen design spec](https://github.com/getlimen/limen/blob/main/docs/superpowers/specs/2026-04-14-limen-design.md).

## License

[Apache 2.0](LICENSE)
