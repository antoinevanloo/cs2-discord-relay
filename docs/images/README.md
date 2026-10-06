# Screenshots

The main README uses these images:

| File | Content |
|---|---|
| `embed-mode.png` | Discord channel in **Embed** mode: a few chat messages, one team message, a connect, a disconnect and a map change |
| `text-mode.png` | The same sequence in **Text** mode (`"MessageFormat": "Text"`) |

## Generating the sequence

No game server needed: the live test posts the same showcase (map change, connections, chat, team chat, a neutralized `@everyone`, a disconnect and a test message) to a real webhook, once per format.

```bash
# bash / zsh
DISCORD_WEBHOOK_URL='https://discord.com/api/webhooks/...' \
  dotnet test -c Release --project tests/Cs2DiscordRelay.Core.Tests -- --filter-trait "Category=Live"
```

```powershell
# PowerShell
$env:DISCORD_WEBHOOK_URL = 'https://discord.com/api/webhooks/...'
dotnet test -c Release --project tests/Cs2DiscordRelay.Core.Tests -- --filter-trait "Category=Live"
```

Use a private test channel: the sequence is posted twice (Embed, then Text). Never commit the URL.

Tips: dark Discord theme, about 900 px wide, crop to the channel area. Blur any personal data you do not want to publish.
