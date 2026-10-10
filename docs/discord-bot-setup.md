# Discord → game chat: setting up the bot

The game → Discord direction only needs a webhook. Showing Discord messages **in game** also needs a **Discord bot**, because a webhook cannot read a channel. It takes about five minutes and is free.

<!-- TODO(LinKeD): add screenshots of the Developer Portal steps in docs/images/ (bot-token.png, bot-intents.png, bot-invite.png). -->

## 1. Create the application

1. Open the [Discord Developer Portal](https://discord.com/developers/applications) and click **New Application**.
2. Name it, for example `CS2 Relay`. Players will see this name in the member list.

## 2. Configure the bot

In the **Bot** tab:

1. Click **Reset Token**, then copy the token. It is shown only once.
   > [!CAUTION]
   > The token gives full control over the bot. Never paste it in a chat, a screenshot or a public repository. If it leaks, click **Reset Token** again: the old one stops working at once.
2. Turn off **Public Bot**, so that only you can add it to a server.
3. Under **Privileged Gateway Intents**, turn on **Message Content Intent**, then click **Save Changes**.
   Without it, Discord refuses the connection and the plugin stops with `close code 4014`.
   Apps with fewer than 10,000 users only need to tick the box: no review by Discord.

## 3. Invite the bot to your server

In the **OAuth2** tab, open **URL Generator**:

1. **Scopes**: tick `bot`.
2. **Bot permissions**: tick **View Channels** and **Read Message History**.
3. Open the generated URL, pick your server and click **Authorize**.

You can also build the URL yourself. The application ID is shown under **General Information**:

```text
https://discord.com/oauth2/authorize?client_id=<APPLICATION_ID>&scope=bot&permissions=66560
```

## 4. Let it see the channel

The bot must be able to **view** the channel it reads. In a private channel, open **Edit Channel → Permissions** and add the bot (or its role) with **View Channel**.

By default, the plugin reads **the channel of `WebhookUrl`**. That gives a two-way chat in one channel, and the relay's own posts are never sent back to the game.

To read another channel, enable **User Settings → Advanced → Developer Mode**, then right-click the channel → **Copy Channel ID**, and put the ID in `DiscordToGame.ChannelId`.

## 5. Configure the plugin

In `addons/counterstrikesharp/configs/plugins/Cs2DiscordRelay/Cs2DiscordRelay.json`:

```jsonc
"DiscordToGame": {
  "Enabled": true,
  "BotToken": "paste the token here",   // secret
  "ChannelId": "",                      // empty = the WebhookUrl channel
  "AllowedRoleIds": []                  // empty = anyone who can write in the channel
}
```

Then run `css_plugins reload Cs2DiscordRelay` and `css_discordrelay_status`. The second line should read:

```text
[Cs2DiscordRelay] Discord to game: Connected as CS2 Relay, reading #cs2-chat.
```

Write something in the channel: it shows up in the game chat within a second.

## Optional: only some roles can talk to the game

Right-click a role (Developer Mode on) → **Copy Role ID**, and add it to `AllowedRoleIds`. Only members with one of these roles then reach the game chat.

## What players see in game

```text
[Discord] Nova: anyone up for a retake?
```

- Mentions, custom emojis, timestamps and markdown are turned into plain text.
- Spoilers are hidden as `[spoiler]`. Attachments show as `[image]` or `[file]`.
- CS2 color codes and line breaks typed in Discord are removed, so nobody can recolor the chat or fake a server message.
- At most `MaxMessagesPer10Seconds` messages (5 by default) reach the game every 10 seconds, with a `UserCooldownSeconds` delay (2 s) per author.
- Nothing is shown while no human player is connected, and messages older than 30 seconds are dropped. A server waking up from hibernation does not replay old messages.
