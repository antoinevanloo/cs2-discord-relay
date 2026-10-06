using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Events;
using Cs2DiscordRelay.Core.Options;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests;

/// <summary>
/// Posts a showcase sequence to a real Discord webhook, in both formats. Skipped unless the
/// <c>DISCORD_WEBHOOK_URL</c> environment variable is set. Handy to check the rendering and to take
/// the README screenshots:
/// <code>DISCORD_WEBHOOK_URL=https://discord.com/api/webhooks/... dotnet test -c Release --project tests/Cs2DiscordRelay.Core.Tests -- --filter-trait "Category=Live"</code>
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveWebhookTests
{
    private const string EnvironmentVariable = "DISCORD_WEBHOOK_URL";

    // Account ID 0: a well-formed SteamID64 that belongs to nobody.
    private const ulong DemoSteamId = 76561197960265728UL;

    [Theory]
    [InlineData(MessageFormat.Embed)]
    [InlineData(MessageFormat.Text)]
    public async Task Showcase_is_accepted_by_discord(MessageFormat format)
    {
        var rawUrl = Environment.GetEnvironmentVariable(EnvironmentVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(rawUrl), $"Set {EnvironmentVariable} to post to a real webhook.");
        Assert.True(WebhookUrl.TryParse(rawUrl, out var url, out var error), error);

        var logger = new ListLogger();
        var options = new RelayOptions { Format = format, WebhookUsername = "CS2 Discord Relay" }.Normalize([]);
        var service = new RelayService(url, options, logger);

        foreach (var relayEvent in Showcase(DateTimeOffset.UtcNow))
        {
            Assert.True(service.Publish(relayEvent));
        }

        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.True(service.IsEnabled, logger.AllText);
        Assert.Empty(logger.Warnings);
    }

    private static IEnumerable<RelayEvent> Showcase(DateTimeOffset now)
    {
        var nova = new PlayerInfo("nova", DemoSteamId, PlayerTeam.CounterTerrorist);
        var ghost = new PlayerInfo("Ghost_", DemoSteamId, PlayerTeam.Terrorist);

        yield return new MapChangedEvent(now, "de_mirage");
        yield return new PlayerConnectedEvent(now, nova);
        yield return new PlayerConnectedEvent(now, ghost);
        yield return new ChatMessageEvent(now, nova, "glhf everyone", TeamOnly: false);
        yield return new ChatMessageEvent(now, nova, "2 B, smoke window", TeamOnly: true);
        yield return new ChatMessageEvent(now, ghost, "@everyone **free** [skins](https://example.com) — no ping, no link", TeamOnly: false);
        yield return new PlayerDisconnectedEvent(now, ghost, DisconnectReasons.Describe(29));
        yield return new RelayTestEvent(now, "Console");
    }
}
