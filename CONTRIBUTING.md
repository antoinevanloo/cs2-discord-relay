# Contributing

Thanks for your interest in **cs2-discord-relay**! Bug reports, ideas and pull requests are welcome.

## Reporting a bug

Please include:
- the CS2, Metamod:Source and CounterStrikeSharp versions;
- the plugin version (`css_plugins list`);
- your configuration file, **with `WebhookUrl` removed**;
- the relevant console or `addons/counterstrikesharp/logs/` lines.

## Development setup

Requirements: [.NET SDK 10](https://dotnet.microsoft.com/download). The exact feature band is pinned in `global.json`.

```bash
dotnet build -c Release   # warnings are errors
dotnet test  -c Release
dotnet publish src/Cs2DiscordRelay -c Release -o out/Cs2DiscordRelay   # folder to copy to a test server
```

## Project layout

| Path | Role |
|---|---|
| `src/Cs2DiscordRelay` | CounterStrikeSharp plugin: event handlers and configuration only |
| `src/Cs2DiscordRelay.Core` | Game-agnostic logic: filtering, formatting, markdown escaping, batching, rate limiting |
| `tests/Cs2DiscordRelay.Core.Tests` | Unit tests for the core |
| `tests/Cs2DiscordRelay.Tests` | Tests for the plugin's configuration shape |

New logic belongs in **Core**, with tests. The plugin layer should stay thin: copy data out of the game objects on the game thread, then hand plain records over to Core.

## Guidelines

- Never block the game thread: no synchronous I/O, no `.Result` / `.Wait()` in event handlers.
- Never log the webhook URL. Use `WebhookUrl.Redact` for any text that might contain it.
- Escape every piece of player-controlled text before it reaches Discord (`DiscordMarkdown`).
- Keep `dotnet build` at **0 warnings**.

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org/): `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `ci`, `build`, `chore`, with an optional scope (`core`, `plugin`).

```text
feat(core): add per-event webhook routing
fix(plugin): ignore HLTV disconnects
```

## Releasing (maintainers)

Push a `vX.Y.Z` tag. The release workflow then:
1. builds and tests;
2. zips the plugin folder;
3. attaches the zip and its SHA-256 checksum to a GitHub Release.
