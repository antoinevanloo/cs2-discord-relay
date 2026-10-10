# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

CounterStrikeSharp (CSS) plugin relaying CS2 chat, connections, disconnections and map changes to a Discord webhook. Public portfolio project: code, tests, README and commit history must stay clean.

- .NET 10, `CounterStrikeSharp.API` 1.0.376 (CSS targets net10 since v1.0.369), `[MinimumApiVersion(333)]`.
- Versions are centralized in `Directory.Packages.props`; build settings in `Directory.Build.props` (nullable, warnings as errors, analyzers in `Recommended` mode, XML docs required on public members).

## Commands

```bash
dotnet build -c Release                       # must stay at 0 warnings
dotnet test  -c Release                       # xUnit v3 on Microsoft.Testing.Platform (see global.json)
dotnet test  -c Release --project tests/Cs2DiscordRelay.Core.Tests -- --filter-class "*WebhookDispatcherTests"
dotnet publish src/Cs2DiscordRelay -c Release -o out/Cs2DiscordRelay   # plugin folder to deploy
```

- Test filters use MTP syntax after `--` (`--filter-class`, `--filter-method`, `--filter-trait`), not VSTest's `--filter`.
- `Category=Live` tests talk to real Discord and are skipped unless `DISCORD_WEBHOOK_URL` (and `DISCORD_BOT_TOKEN`, optionally `DISCORD_CHANNEL_ID`, for the gateway test) are set. Never hardcode or log a webhook URL or a bot token.
- The published folder must contain only `Cs2DiscordRelay.dll`, `Cs2DiscordRelay.Core.dll`, the deps file and the pdbs. CSS and `Microsoft.Extensions.*` abstractions are provided by the host (`ExcludeAssets="runtime"` in the plugin csproj).

## Architecture

- `src/Cs2DiscordRelay.Core`: no CSS dependency, fully unit-tested.
  - `Events/`: immutable `RelayEvent` records, `PresenceTracker` (hides map-change disconnect/reconnect pairs), `DisconnectReasons`.
  - `Options/`: `RelayOptions` and its `Normalize`, which fixes invalid values and reports one warning per fix.
  - `Formatting/`: `EventFilter`, `MessageFormatter` (text or embed), `DiscordMarkdown` (escape, sanitize, truncate), `PayloadBuilder` (Discord limits).
  - `Discord/`: `WebhookUrl` (validation and redaction), `WebhookDispatcher` (bounded queue, batching, 429/bucket/5xx handling), `RateLimitPolicy`, source-generated JSON payloads.
  - `RelayService`: the facade the plugin uses for game → Discord.
  - `Discord/Gateway/`: hand-written gateway client (`GatewayClient`: identify, jittered heartbeat with zombie detection, resume, reconnect, fatal close codes) over `IGatewayTransport` (`WebSocketGatewayTransport` in production). `Discord/DiscordRestClient`, `Discord/BotToken`.
  - `Inbound/`: Discord → game pipeline. `DiscordMessage` (wire → plain record), `InboundFilter` (channel, no bots/webhooks = no echo, roles), `GameChatFormatter` (markup → plain text, strips CS2 color control chars), `GameChatTemplate`, `InboundRateLimiter`, `InboundQueue` (bounded, time-stamped).
  - `DiscordBridgeService`: the facade for Discord → game. `Drain` is the only call made from the game thread.
- `src/Cs2DiscordRelay`: thin CSS layer. It copies game data into `RelayEvent`s on the game thread, then calls `RelayService.Publish`. It also holds the config classes (`PluginConfig`, `DiscordToGameConfig`, `ConfigVersion` 2), the admin commands, and a 0.5 s `AddTimer` that drains the bridge and calls `Server.PrintToChatAll`.
- `tests/Cs2DiscordRelay.Core.Tests`: delays go through an injected `Func<TimeSpan, CancellationToken, Task>` plus `FakeTimeProvider` (`Fakes/DispatcherHarness`), so tests never sleep. Gateway tests use `Fakes/GatewayHarness`: scripted `FakeGatewayTransport` connections and `ManualDelays` the test elapses explicitly; use `SyncAsync` before elapsing a delay that depends on frames just pushed.
- `tests/Cs2DiscordRelay.Tests`: config JSON shape and reflection checks on the plugin. `BasePlugin`'s constructor calls native code, so never instantiate the plugin in tests.

## Rules

- **Verify every CSS API against the source or the package** (`~/.nuget/packages/counterstrikesharp.api/<ver>/lib/net10.0/*.xml`, or `gh api repos/roflmuffin/CounterStrikeSharp/contents/<path>?ref=v<ver>`). Never guess. Known pitfalls:
  - command handlers must be **public**, because CSS scans them with `GetMethods()`;
  - `css_plugins reload` resolves plugins by `ModuleName`, which must stay `Cs2DiscordRelay` (same as the DLL and folder);
  - `GameEvent` objects are only valid inside the handler.
- Never block the game thread: no network I/O, `.Result` or `.Wait()` in handlers. `Publish` only enqueues.
- Never call CSS natives (chat, players, commands) from a background thread: CSS throws "Invoked on a non-main thread". Background code hands data over through queues; the game thread polls them (timer). Do not use `Server.NextFrame` for this: its queue is unbounded and paused while the server hibernates.
- Never log the bot token: use `BotToken.Redact`. Test tokens must not look like real ones (GitHub push protection), see `TestData.BotToken`.
- Escape all player-controlled text with `DiscordMarkdown`. `allowed_mentions` stays empty.
- Never log the webhook URL: use `WebhookUrl.Redact` on any external text (exception messages, Discord errors).
- Logging goes through `[LoggerMessage]` partial methods (`Discord/Log.cs`, `PluginLog.cs`).
- New behavior goes into Core with tests; keep the plugin layer thin.
- Files use LF line endings (`.gitattributes`); this matters for the sibling server repo's shell hooks.

## Git

- Conventional Commits in English, scopes `core`, `plugin`, `tests`, `release`. See `.claude/skills/commit/SKILL.md`.
- Commit author is `LinKeD <linked.staff@gmail.com>` (repo-local git config).
- **Never mention AI in commits or PRs: no `Co-Authored-By` trailer, no "Generated with".**
- Ask before any push, tag or GitHub release.
- Releases: push a `vX.Y.Z` tag. `.github/workflows/release.yml` tests, publishes, zips `Cs2DiscordRelay/` and attaches it with a SHA-256 checksum. Move the `CHANGELOG.md` entries from `Unreleased` to the version first.

## Manual testing

The private sibling repo `../cs2-discord-relay-server` provides a Docker CS2 server (joedwards32/cs2, Metamod, CSS). Use `scripts/deploy-plugin.sh` to publish this plugin into it, and `scripts/rcon.py` to run console commands such as `css_discordrelay_status`.
