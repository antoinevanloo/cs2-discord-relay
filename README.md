<div align="center">

# CS2 Discord Relay

**Stream your Counter-Strike 2 server's chat and events to a Discord channel, and optionally bring Discord messages back into the game. Lightweight, rate-limit aware, safe by default.**

[![CI](https://github.com/antoinevanloo/cs2-discord-relay/actions/workflows/ci.yml/badge.svg)](https://github.com/antoinevanloo/cs2-discord-relay/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/antoinevanloo/cs2-discord-relay?include_prereleases&sort=semver)](https://github.com/antoinevanloo/cs2-discord-relay/releases)
[![License: MIT](https://img.shields.io/github/license/antoinevanloo/cs2-discord-relay)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![CounterStrikeSharp](https://img.shields.io/badge/CounterStrikeSharp-v1.0.376-orange)](https://github.com/roflmuffin/CounterStrikeSharp)

</div>

---

## Preview

<!--
  TODO(LinKeD): add real Discord screenshots in docs/images/ (see docs/images/README.md):
    - embed-mode.png : a few chat messages, a team message, a connect/disconnect and a map change in Embed mode
    - text-mode.png  : the same sequence in Text mode
  Recommended width: ~900 px, dark Discord theme, crop to the channel area.
-->

| Embed mode | Text mode |
|:---:|:---:|
| ![Embed mode in Discord](docs/images/embed-mode.png) | ![Text mode in Discord](docs/images/text-mode.png) |

## Features

- 💬 **Chat relay**: global chat and team chat, with a distinct `[TEAM]` prefix and color.
- 🟢 **Connections and disconnections**, with the reason (timed out, kicked, banned...) and an optional link to the player's Steam profile.
- 🔁 **No map-change spam**: CS2 disconnects and reconnects every player on `changelevel`. These transient events are recognized and hidden, so only real arrivals and departures are announced.
- 🗺️ **Map changes**.
- 🔄 **Optional Discord → game chat**: with a Discord bot, messages written in the channel appear in the game chat (`[Discord] Nova: gg`). Mentions, emojis and markdown become plain text, CS2 color codes are stripped, and a rate limit keeps the chat readable. [Setup guide](docs/discord-bot-setup.md).
- 🎛️ **Every event type can be toggled** individually.
- 🎨 **Two formats**: rich **embeds** with one color per event type, or compact **text** lines.
- 🚫 **Command filter**: chat commands such as `!rtv` or `/ws` are not relayed (prefixes are configurable).
- 🤖 **Bot filter**, on by default.
- 🛡️ **Safe by default**:
  - `@everyone`, `@here` and every other mention are neutralized (`allowed_mentions` is always empty);
  - player names and messages are markdown-escaped;
  - invisible and bidi characters are stripped;
  - the webhook URL is **never** written to logs.
- ⚡ **Never blocks the game thread**:
  - events are queued and sent from a background loop through a single reused `HttpClient`;
  - bursts are grouped into one request;
  - Discord rate limits (`429`, `retry_after`, exhausted buckets) are honoured;
  - transient errors are retried with exponential backoff.
- 🧯 **Fails gracefully**: a missing or invalid webhook logs a clear warning and the plugin stays idle. It never crashes the server.

## Requirements

| Component | Version |
|---|---|
| Counter-Strike 2 dedicated server | Linux or Windows |
| [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) | 2.x (dev builds) |
| [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) | **API v1.0.333 or newer** (uses the `OnPlayerChat` listener). Built against **v1.0.376**, which runs on .NET 10 |
| **Tested with** | CounterStrikeSharp **v1.0.376** on a native **Windows** dedicated server: global and team chat, connections and disconnections, map changes (no reconnect spam), admin commands, Discord → game chat through a bot |

> [!TIP]
> Install the CounterStrikeSharp build **with runtime** unless your server already ships the .NET runtime.

## Installation

1. Install **Metamod:Source** and **CounterStrikeSharp** on your server. The [CounterStrikeSharp getting started guide](https://docs.cssharp.dev/docs/guides/getting-started.html) covers both.
2. Download `cs2-discord-relay-vX.Y.Z.zip` from the [latest release](https://github.com/antoinevanloo/cs2-discord-relay/releases/latest).
   The same zip works on **Linux and Windows** servers: the plugin is platform-independent .NET code. Only Metamod and CounterStrikeSharp have separate Linux and Windows builds.
3. Extract it into `game/csgo/addons/counterstrikesharp/plugins/`. You should end up with:
   ```text
   game/csgo/addons/counterstrikesharp/plugins/Cs2DiscordRelay/
   ├── Cs2DiscordRelay.dll
   ├── Cs2DiscordRelay.Core.dll
   ├── Cs2DiscordRelay.deps.json
   └── *.pdb
   ```
   > The folder name **must** match the DLL name (`Cs2DiscordRelay`). CounterStrikeSharp ignores it otherwise.
4. Restart the server. On first load the plugin creates its configuration file, logs a warning that the webhook is not set yet, and stays idle:
   ```text
   game/csgo/addons/counterstrikesharp/configs/plugins/Cs2DiscordRelay/Cs2DiscordRelay.json
   ```
5. [Create a Discord webhook](#creating-a-discord-webhook) and paste its URL into `WebhookUrl`.
6. Apply the change without restarting, from the server console or RCON:
   ```text
   css_plugins reload Cs2DiscordRelay
   ```
   The console should print `Discord relay started (Embed messages).`
7. Check the webhook end to end:
   ```text
   css_discordrelay_test
   ```
   A "✅ Discord relay is working" message should appear in your channel within a few seconds.

## Configuration

The file is generated with these defaults. Comments (`//`) are allowed: CounterStrikeSharp skips them when reading the file.

```jsonc
{
  "WebhookUrl": "",                 // Discord webhook URL. Keep it secret!
  "MessageFormat": "Embed",         // "Embed" or "Text"
  "Events": {
    "Chat": true,                   // global chat (say)
    "TeamChat": true,               // team chat (say_team)
    "PlayerConnect": true,
    "PlayerDisconnect": true,
    "MapChange": true
  },
  "IgnoreCommands": true,           // do not relay messages starting with a CommandPrefixes entry
  "CommandPrefixes": [ "!", "/" ],
  "IgnoreBots": true,
  "ShowSteamId": true,              // SteamID64 + link to the Steam profile
  "Colors": {                       // embed colors, #RRGGBB
    "Chat": "#5865F2",
    "TeamChat": "#FEE75C",
    "PlayerConnect": "#57F287",
    "PlayerDisconnect": "#ED4245",
    "MapChange": "#EB459E"
  },
  "WebhookUsername": "",            // overrides the webhook name, empty = name set in Discord
  "WebhookAvatarUrl": "",           // overrides the webhook avatar, empty = avatar set in Discord
  "BatchWindowMs": 1000,            // wait this long to group bursts into one message, 0 = send immediately
  "MaxQueueSize": 500,              // pending messages kept while Discord is slow, oldest dropped first
  "DiscordToGame": {                // optional, needs a Discord bot: see docs/discord-bot-setup.md
    "Enabled": false,
    "BotToken": "",                 // bot token. Keep it secret!
    "ChannelId": "",                // channel to read, empty = the WebhookUrl channel
    "AllowedRoleIds": [],           // empty = anyone who can write in the channel
    "GameChatFormat": "{LightBlue}[Discord]{Default} {Green}{Name}{Default}: {Message}",
    "MaxMessageLength": 200,
    "MaxMessagesPer10Seconds": 5,
    "UserCooldownSeconds": 2
  },
  "ConfigVersion": 2                // managed by CounterStrikeSharp, do not change
}
```

| Option | Type | Default | Description |
|---|---|---|---|
| `WebhookUrl` | string | `""` | Discord webhook URL (`https://discord.com/api/webhooks/<id>/<token>`). To post into a forum thread, append `?thread_id=<id>`. While it is empty or invalid, the relay stays idle and logs a warning. |
| `MessageFormat` | `"Embed"` \| `"Text"` | `"Embed"` | `Embed` gives rich cards with one color per event. `Text` gives compact lines (`💬 [TEAM] **Player** (Terrorists): rush B`), with link previews disabled. |
| `Events.*` | bool | `true` | Turns each event type on or off. `Chat` and `TeamChat` are independent. |
| `IgnoreCommands` | bool | `true` | Skips chat messages starting with one of `CommandPrefixes`. Leading spaces are ignored. |
| `CommandPrefixes` | string[] | `["!", "/"]` | Prefixes that identify chat commands. |
| `IgnoreBots` | bool | `true` | Skips connections, disconnections and chat from bots. |
| `ShowSteamId` | bool | `true` | Adds the SteamID64 and a link to the player's Steam profile. |
| `Colors.*` | `#RRGGBB` | see above | Embed accent color per event type. Invalid values fall back to the default, with a warning. |
| `WebhookUsername` | string | `""` | Display name of the messages (max. 80 characters). |
| `WebhookAvatarUrl` | string | `""` | Absolute `http(s)` URL of an avatar image. |
| `BatchWindowMs` | int | `1000` | Grouping window for bursts, between `0` and `10000`. Higher values mean fewer requests but more latency. |
| `MaxQueueSize` | int | `500` | Queue capacity, between `10` and `10000`. When Discord cannot keep up, the oldest messages are dropped and a warning is logged at most every 30 s. |

### Discord → game chat (optional)

Off by default. It needs a Discord bot with the **Message Content Intent**: follow the [setup guide](docs/discord-bot-setup.md), about five minutes.

| Option | Type | Default | Description |
|---|---|---|---|
| `DiscordToGame.Enabled` | bool | `false` | Turns the Discord → game relay on. |
| `DiscordToGame.BotToken` | string | `""` | Bot token from **Developer Portal → Bot → Reset Token**. Never logged. |
| `DiscordToGame.ChannelId` | string | `""` | ID of the channel (or thread) to read. Empty reads the channel of `WebhookUrl`, which makes it a two-way chat; the relay's own posts are never sent back. |
| `DiscordToGame.AllowedRoleIds` | string[] | `[]` | When set, only members with one of these roles reach the game chat. |
| `DiscordToGame.GameChatFormat` | string | see above | In-game line. Color tags are CounterStrikeSharp's (`{Green}`, `{LightBlue}`, `{Gold}`...); `{Name}` and `{Message}` are replaced by the author and the text, which are never interpreted. |
| `DiscordToGame.MaxMessageLength` | int | `200` | Longer messages are truncated, between `20` and `400`. |
| `DiscordToGame.MaxMessagesPer10Seconds` | int | `5` | Messages shown in game per 10 seconds, all authors together, between `1` and `50`. |
| `DiscordToGame.UserCooldownSeconds` | int | `2` | Minimum delay between two messages of the same author, between `0` and `60`. |

Messages are shown only while a human player is connected, and messages older than 30 seconds are dropped. Configuration files from v1.0 (`"ConfigVersion": 1`) keep working: add the `DiscordToGame` section to use it.

After editing the file, run `css_plugins reload Cs2DiscordRelay`.

## Admin commands

Both commands work from the server console and RCON. In game, they require the `@css/root` permission (see [CounterStrikeSharp admin configuration](https://docs.cssharp.dev/docs/admin-framework/defining-admins.html)) and can be typed in chat as `!discordrelay_test` / `!discordrelay_status`.

| Command | Description |
|---|---|
| `css_discordrelay_test` | Sends a test message to the webhook, bypassing the event toggles. Delivery errors are written to the server log. |
| `css_discordrelay_status` | Prints the version and the state of both directions: running or disabled (and why), format, relayed events, messages waiting to be sent, and the Discord bot connection. |

```text
] css_discordrelay_status
[Cs2DiscordRelay] v1.1.0. Game to Discord: Running: Embed format, relaying chat, team chat, connections, disconnections, map changes. 0 message(s) waiting to be sent.
[Cs2DiscordRelay] Discord to game: Connected as CS2 Relay, reading #cs2-chat.
```

## Creating a Discord webhook

1. In Discord, open **Server Settings → Integrations → Webhooks**. You need the *Manage Webhooks* permission.
2. Click **New Webhook**, give it a name and an avatar, and pick the target channel.
3. Click **Copy Webhook URL** and paste it into `WebhookUrl`.

> [!WARNING]
> Anyone who has the webhook URL can post in your channel. Do not commit it or share it. If it leaks, delete the webhook in Discord and create a new one.

## Troubleshooting

> [!TIP]
> Start with `css_discordrelay_status`: it tells you whether the relay is running and, if not, why.

<details>
<summary><b>The plugin does not load</b></summary>

- Run `meta list` in the server console. CounterStrikeSharp must be listed.
- Run `css_plugins list`. `Cs2DiscordRelay` should appear.
- Check the folder layout: `plugins/Cs2DiscordRelay/Cs2DiscordRelay.dll`. The folder and the DLL must have the same name, and `Cs2DiscordRelay.Core.dll` must sit next to it.
- `requires a newer version of CounterStrikeSharp` means you need to update CounterStrikeSharp to API v1.0.333 or newer.
- Look for errors in `addons/counterstrikesharp/logs/`.

</details>

<details>
<summary><b>"WebhookUrl is empty" / "does not look like https://discord.com/api/webhooks/..."</b></summary>

The URL is missing or malformed.
1. Copy it again from Discord (**Copy Webhook URL**). It must start with `https://discord.com/api/webhooks/`.
2. Paste it into the config file.
3. Run `css_plugins reload Cs2DiscordRelay`.

</details>

<details>
<summary><b>"Discord rejected the webhook (HTTP 404) ... Relay disabled"</b></summary>

The webhook was deleted, or its token is wrong. The relay stops sending so it does not spam Discord.
1. Create a new webhook and update `WebhookUrl`.
2. Reload the plugin.

</details>

<details>
<summary><b>Messages arrive late, or "Relay queue is full: N message(s) were dropped"</b></summary>

Discord limits each webhook to a few requests per second. The plugin already groups bursts and waits whenever Discord asks it to (HTTP 429). If you still see this warning on a very busy server:
- increase `BatchWindowMs` (for example to `2000`);
- turn off noisy events (`PlayerConnect` / `PlayerDisconnect` on servers with a high player turnover);
- give each server its own webhook.

</details>

<details>
<summary><b>Discord to game: "close code 4014" / "Message Content Intent is not enabled"</b></summary>

In the [Developer Portal](https://discord.com/developers/applications), open your application → **Bot** → **Privileged Gateway Intents**, turn on **Message Content Intent** and save. Then run `css_plugins reload Cs2DiscordRelay`.

</details>

<details>
<summary><b>Discord to game: "Discord rejected DiscordToGame.BotToken (HTTP 401)" / "close code 4004"</b></summary>

The token is wrong or was reset. Click **Reset Token** in the **Bot** tab, paste the new token into `DiscordToGame.BotToken` and reload the plugin. The token is not the application ID nor the client secret.

</details>

<details>
<summary><b>Discord to game: "The bot cannot see channel ..."</b></summary>

The bot is not on the server, or the channel is private. [Invite the bot](docs/discord-bot-setup.md#3-invite-the-bot-to-your-server), then give it **View Channel** in the channel permissions. If `ChannelId` is set, check it is the channel ID (Developer Mode → right-click → **Copy Channel ID**), not the server ID.

</details>

<details>
<summary><b>Discord to game: connected, but nothing shows up in game</b></summary>

- Messages are shown only while at least one human player is connected.
- Messages from bots and webhooks are ignored on purpose.
- With `AllowedRoleIds` set, the author needs one of these roles.
- Busy channels hit the rate limit (`MaxMessagesPer10Seconds`, `UserCooldownSeconds`); the server log says when messages were skipped.

</details>

<details>
<summary><b>Some messages are not relayed</b></summary>

This is usually caused by a filter:
- chat commands (`IgnoreCommands`, `CommandPrefixes`);
- bots (`IgnoreBots`);
- an event turned off in `Events`.

`css_discordrelay_test` bypasses every filter: if the test message arrives but chat does not, check the filters above.

Only messages typed by players are relayed. Messages sent from the server console are not.

</details>

## How it works

```text
 game thread (CounterStrikeSharp)            background                      Discord
 ────────────────────────────────            ──────────                      ───────
 OnPlayerChat / player_connect_full ─┐
 player_disconnect / OnMapStart     ─┼─▶ copy to immutable RelayEvent
                                     │   filter ─▶ format (escape, embed/text)
                                     └─▶ bounded queue (non-blocking) ─▶ batch ─▶ POST webhook
                                                                         ▲          │
                                                                         └─ 429 / 5xx: wait & retry

 Optional Discord → game direction:

 timer (every 0.5 s) ◀── bounded, time-stamped queue ◀── rate limit ◀── plain text ◀── filter ◀── gateway (WebSocket, bot)
   └─ PrintToChatAll, only when a human player is connected                   (no bots, no webhooks: no echo)
```

- `src/Cs2DiscordRelay`: thin CounterStrikeSharp layer (events, config, admin commands, chat printing on the game thread).
- `src/Cs2DiscordRelay.Core`: formatting, escaping, batching, rate limiting, and a hand-written Discord gateway client (heartbeat, resume, reconnect) with no third-party dependency. It does not depend on the game, which keeps it fully unit-tested.
- `tests/`: xUnit v3 test suites (formatting, markdown escaping, queue and retry logic, gateway sessions with scripted connections, config shape).

## Roadmap

Ideas for the next versions. Feedback and pull requests are welcome.

- [x] **Two-way relay**: Discord messages displayed in game (v1.1, [setup guide](docs/discord-bot-setup.md)).
- [ ] **Admin commands from Discord** (kick, ban, map change, rcon) with role-based permissions.
- [ ] **Live server status**: a message updated in real time with the map, score and player list.
- [ ] **One webhook per event type**: for example chat in `#cs2-chat` and connections in `#cs2-logs`.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md). In short:

```bash
dotnet build -c Release   # must stay at 0 warnings
dotnet test  -c Release
```

Commits follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat:`, `fix:`, `docs:`, `ci:`...).

Found a security issue (mention bypass, webhook URL leak...)? Please report it privately, see [SECURITY.md](SECURITY.md).

## License

[MIT](LICENSE) © LinKeD

This plugin is compiled against the [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) API, which is distributed under GPL-3.0 and is **not** bundled in the release. Like most plugins in the ecosystem, this project is MIT licensed. Check the CounterStrikeSharp license if this matters for your use case.

---

## Need a custom plugin?

I build **custom CounterStrikeSharp plugins for CS2 servers**: gameplay features, admin tools, integrations with Discord, databases or web APIs, and performance fixes on existing plugins. I deliver clean, tested and documented code, like this repository.

📩 **Contact:** <!-- TODO(LinKeD): replace with your contact --> `YOUR_CONTACT_HERE` (Discord / email / website)
