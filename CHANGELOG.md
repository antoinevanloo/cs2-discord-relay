# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-10-10

### Added
- Relay of global and team chat (`[TEAM]` prefix), player connections and disconnections (with reason), and map changes to a Discord webhook.
- Map-change reconnects (transient `player_disconnect` + reconnection) are not announced.
- Admin commands `css_discordrelay_test` (sends a test message) and `css_discordrelay_status` (state of the relay), restricted to `@css/root` in game.
- Embed or text formats, with a configurable color per event type.
- Per-event toggles, chat command filter (`!`, `/`) and bot filter.
- Non-blocking background queue with burst batching, Discord rate limit handling (429 `retry_after`, exhausted buckets) and exponential backoff.
- Mention neutralization (`allowed_mentions` always empty), markdown escaping, and redaction of the webhook URL in logs.
- CI (build and test on every push or PR) and a release workflow producing a ready-to-install zip.

[Unreleased]: https://github.com/antoinevanloo/cs2-discord-relay/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/antoinevanloo/cs2-discord-relay/releases/tag/v1.0.0
