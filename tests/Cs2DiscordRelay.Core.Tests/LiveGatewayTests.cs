using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Inbound;
using Cs2DiscordRelay.Core.Options;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests;

/// <summary>
/// Connects the Discord → game bridge to the real gateway. Skipped unless <c>DISCORD_BOT_TOKEN</c> is set,
/// together with <c>DISCORD_WEBHOOK_URL</c> (its channel is read) or <c>DISCORD_CHANNEL_ID</c>.
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveGatewayTests
{
    [Fact]
    public async Task Connects_and_ignores_the_relay_own_posts()
    {
        var rawToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        var rawWebhook = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
        var rawChannel = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(rawToken) || (string.IsNullOrWhiteSpace(rawWebhook) && string.IsNullOrWhiteSpace(rawChannel)),
            "Set DISCORD_BOT_TOKEN and DISCORD_WEBHOOK_URL (or DISCORD_CHANNEL_ID) to connect to the real gateway.");

        Assert.True(BotToken.TryParse(rawToken, out var token, out var tokenError), tokenError);
        WebhookUrl? webhook = null;
        if (!string.IsNullOrWhiteSpace(rawWebhook))
        {
            Assert.True(WebhookUrl.TryParse(rawWebhook, out webhook, out var webhookError), webhookError);
        }

        var logger = new ListLogger();
        var options = new InboundOptions { ChannelId = Snowflake.TryParse(rawChannel, out var channelId) ? channelId : null }.Normalize([]);
        await using var bridge = new DiscordBridgeService(token, options, webhook, logger);
        bridge.Start();

        await WaitUntilAsync(() => bridge.IsStopped || bridge.DescribeStatus().StartsWith("Connected", StringComparison.Ordinal), TimeSpan.FromSeconds(30));
        Assert.True(bridge.DescribeStatus().StartsWith("Connected", StringComparison.Ordinal), bridge.DescribeStatus() + "\n" + logger.AllText);

        if (webhook is not null)
        {
            // Posts through the webhook, as the game → Discord relay does: it must not come back to the game.
            await using var relay = new RelayService(webhook, new RelayOptions { BatchWindow = TimeSpan.Zero }.Normalize([]), logger);
            Assert.True(relay.Publish(new Events.RelayTestEvent(DateTimeOffset.UtcNow, "LiveGatewayTests")));
            await Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            var lines = new List<GameChatLine>();
            bridge.Drain(lines);
            Assert.Empty(lines);
        }

        Assert.DoesNotContain(token.Value, logger.AllText, StringComparison.Ordinal);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
