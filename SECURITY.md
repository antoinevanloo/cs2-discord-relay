# Security policy

## Supported versions

Only the [latest release](https://github.com/antoinevanloo/cs2-discord-relay/releases/latest) receives fixes.

## Reporting a vulnerability

Please **do not open a public issue**. Report it privately through [GitHub security advisories](https://github.com/antoinevanloo/cs2-discord-relay/security/advisories/new) instead. You should get an answer within a week.

Examples of what is in scope:
- a way for a player to ping `@everyone`, a role or a user, or to inject links or markdown, through their name or a chat message;
- the webhook URL or its token showing up in logs, console output or error messages;
- a way for a player to crash or freeze the game server through the plugin.

## Your webhook URL is a secret

Anyone who knows a webhook URL can post to your channel. Never share your configuration file without removing `WebhookUrl` first. If it leaks, delete the webhook in Discord (**Server Settings → Integrations → Webhooks**), create a new one, and update the configuration.
