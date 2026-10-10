using System.Net;
using System.Threading.Channels;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Inbound;
using Cs2DiscordRelay.Core.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Cs2DiscordRelay.Core.Tests;

public sealed class DiscordBridgeServiceTests
{
    private const string UserJson = """{"id":"42","username":"RelayBot","bot":true}""";
    private const string ChannelJson = """{"id":"555","name":"cs2-chat","guild_id":"1"}""";
    private const string GatewayJson = """{"url":"wss://gateway.test"}""";

    private static HttpResponseMessage Ok(string json) => FakeHttpHandler.Status(HttpStatusCode.OK, json);

    private sealed class Bridge : IAsyncDisposable
    {
        private readonly Channel<FakeGatewayTransport> _connections = Channel.CreateUnbounded<FakeGatewayTransport>();

        public Bridge(InboundOptions options, string? webhook, params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        {
            Assert.True(BotToken.TryParse(TestData.BotToken, out var token, out _));
            WebhookUrl? url = null;
            if (webhook is not null)
            {
                Assert.True(WebhookUrl.TryParse(webhook, out url, out _));
            }

            Handler = new FakeHttpHandler(responses);
            Service = new DiscordBridgeService(
                token,
                options.Normalize([]),
                url,
                Logger,
                new HttpClient(Handler),
                () =>
                {
                    var transport = new FakeGatewayTransport();
                    _connections.Writer.TryWrite(transport);
                    return transport;
                },
                Delays.Delay,
                () => 0.5,
                Time);
            Service.Start();
        }

        public DiscordBridgeService Service { get; }

        public FakeHttpHandler Handler { get; }

        public ListLogger Logger { get; } = new();

        public ManualDelays Delays { get; } = new();

        public FakeTimeProvider Time { get; } = new(TestData.Now);

        public async Task<FakeGatewayTransport> ConnectedAsync()
        {
            var connection = await _connections.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await GatewayHarness.WaitUntilAsync(() => connection.ConnectedTo is not null);
            connection.Hello();
            await connection.NextSentAsync(2);
            connection.Ready();
            await GatewayHarness.WaitUntilAsync(() => Service.DescribeStatus().StartsWith("Connected", StringComparison.Ordinal));
            return connection;
        }

        public async Task<string> StoppedAsync()
        {
            await GatewayHarness.WaitUntilAsync(() => Service.IsStopped);
            return Service.DescribeStatus();
        }

        public ValueTask DisposeAsync() => Service.DisposeAsync();
    }

    private static string MessageJson(string content, string authorId = "7", string channelId = "555", string extra = "") =>
        $$"""{"id":"{{Guid.NewGuid():N}}","channel_id":"{{channelId}}","type":0,"content":"{{content}}","author":{"id":"{{authorId}}","username":"nova"}{{extra}}}""";

    private static async Task<List<GameChatLine>> DrainWhenAsync(DiscordBridgeService service, int count)
    {
        var lines = new List<GameChatLine>();
        await GatewayHarness.WaitUntilAsync(() =>
        {
            service.Drain(lines);
            return lines.Count >= count;
        });
        return lines;
    }

    [Fact]
    public async Task Relays_messages_of_the_configured_channel()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, webhook: null, _ => Ok(UserJson), _ => Ok(ChannelJson), _ => Ok(GatewayJson));
        var connection = await bridge.ConnectedAsync();

        connection.Dispatch("MESSAGE_CREATE", 2, MessageJson("from another channel", channelId: "999"));
        connection.Dispatch("MESSAGE_CREATE", 3, MessageJson("gl hf <@42>", extra: ""","mentions":[{"id":"42","username":"RelayBot"}]"""));

        var line = Assert.Single(await DrainWhenAsync(bridge.Service, 1));
        Assert.Equal(new GameChatLine("nova", "gl hf @RelayBot"), line);
        Assert.Equal("Connected as RelayBot, reading #cs2-chat.", bridge.Service.DescribeStatus());
        Assert.Equal("wss://gateway.test/?v=10&encoding=json", connection.ConnectedTo!.AbsoluteUri);
        Assert.Contains("Discord to game relay: reading #cs2-chat as RelayBot.", bridge.Logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reads_the_webhook_channel_by_default_and_ignores_its_own_posts()
    {
        await using var bridge = new Bridge(
            new InboundOptions(),
            DispatcherHarness.RawUrl,
            _ => Ok(UserJson),
            _ => Ok("""{"id":"123456789012345678","channel_id":"555"}"""),
            _ => Ok(ChannelJson),
            _ => Ok(GatewayJson));
        var connection = await bridge.ConnectedAsync();

        connection.Dispatch("MESSAGE_CREATE", 2, MessageJson("💬 relayed from the game", authorId: "123456789012345678", extra: ""","webhook_id":"123456789012345678" """));
        connection.Dispatch("MESSAGE_CREATE", 3, MessageJson("typed in discord"));

        var line = Assert.Single(await DrainWhenAsync(bridge.Service, 1));
        Assert.Equal("typed in discord", line.Message);
        Assert.Equal(DispatcherHarness.RawUrl, bridge.Handler.RequestList[1].Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Forum_thread_webhooks_read_their_thread()
    {
        await using var bridge = new Bridge(
            new InboundOptions(),
            DispatcherHarness.RawUrl + "?thread_id=987654321098765432",
            _ => Ok(UserJson),
            _ => Ok("""{"id":"987654321098765432","name":"match-chat"}"""),
            _ => Ok(GatewayJson));
        await bridge.ConnectedAsync();

        Assert.EndsWith("channels/987654321098765432", bridge.Handler.RequestList[1].Uri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Applies_the_rate_limit()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null, _ => Ok(UserJson), _ => Ok(ChannelJson), _ => Ok(GatewayJson));
        var connection = await bridge.ConnectedAsync();

        connection.Dispatch("MESSAGE_CREATE", 2, MessageJson("first"));
        connection.Dispatch("MESSAGE_CREATE", 3, MessageJson("spam")); // same author, within the 2 s cooldown
        await GatewayHarness.WaitUntilAsync(() => bridge.Logger.AllText.Contains("1 message(s) were not shown in game", StringComparison.Ordinal));

        Assert.Equal("first", Assert.Single(await DrainWhenAsync(bridge.Service, 1)).Message);
    }

    [Fact]
    public async Task Stale_lines_are_not_shown()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null, _ => Ok(UserJson), _ => Ok(ChannelJson), _ => Ok(GatewayJson));
        var connection = await bridge.ConnectedAsync();
        connection.Dispatch("MESSAGE_CREATE", 2, MessageJson("while hibernating"));
        await GatewayHarness.WaitUntilAsync(() => bridge.Service.PendingCount == 1);

        bridge.Time.Advance(InboundOptions.MaxAge + TimeSpan.FromSeconds(1));
        var lines = new List<GameChatLine>();

        Assert.Equal(0, bridge.Service.Drain(lines));
        Assert.Equal(0, bridge.Service.PendingCount);
    }

    [Fact]
    public async Task Stops_with_a_clear_message_when_the_token_is_rejected()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null,
            _ => FakeHttpHandler.Status(HttpStatusCode.Unauthorized, """{"message":"401: Unauthorized","code":0}"""));

        var status = await bridge.StoppedAsync();

        Assert.StartsWith("Stopped: Discord rejected DiscordToGame.BotToken (HTTP 401)", status, StringComparison.Ordinal);
        Assert.Contains("Discord rejected DiscordToGame.BotToken", bridge.Logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stops_when_the_bot_cannot_see_the_channel()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null,
            _ => Ok(UserJson),
            _ => FakeHttpHandler.Status(HttpStatusCode.Forbidden, """{"message":"Missing Access","code":50001}"""));

        Assert.Equal(
            "Stopped: The bot cannot see channel 555 (HTTP 403: Missing Access). Invite it to the server and allow it to View Channel there.",
            await bridge.StoppedAsync());
    }

    [Fact]
    public async Task Stops_when_no_channel_can_be_found()
    {
        await using var bridge = new Bridge(new InboundOptions(), null, _ => Ok(UserJson));

        Assert.Equal("Stopped: Set DiscordToGame.ChannelId, or a valid WebhookUrl to read its channel.", await bridge.StoppedAsync());
    }

    [Fact]
    public async Task Retries_while_discord_is_unreachable()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null,
            _ => throw new HttpRequestException("no route to host"),
            _ => Ok(UserJson),
            _ => Ok(ChannelJson),
            _ => Ok(GatewayJson));

        await bridge.Delays.ElapseAsync(TimeSpan.FromSeconds(1));
        await bridge.ConnectedAsync();

        Assert.Contains("cannot reach Discord (HttpRequestException: no route to host)", bridge.Logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reports_gateway_fatal_errors()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null, _ => Ok(UserJson), _ => Ok(ChannelJson), _ => Ok(GatewayJson));
        var connection = await bridge.ConnectedAsync();

        connection.ServerClose(4014);

        Assert.StartsWith("Stopped: Message Content Intent is not enabled", await bridge.StoppedAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Never_logs_the_token()
    {
        await using var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null,
            _ => throw new HttpRequestException($"boom {TestData.BotToken}"),
            _ => FakeHttpHandler.Status(HttpStatusCode.Unauthorized));

        await bridge.Delays.ElapseAsync(TimeSpan.FromSeconds(1));
        await bridge.StoppedAsync();

        Assert.DoesNotContain(TestData.BotToken, bridge.Logger.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain(TestData.BotToken, bridge.Service.DescribeStatus(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dispose_ends_the_gateway_session()
    {
        var bridge = new Bridge(new InboundOptions { ChannelId = 555 }, null, _ => Ok(UserJson), _ => Ok(ChannelJson), _ => Ok(GatewayJson));
        var connection = await bridge.ConnectedAsync();

        await bridge.DisposeAsync();

        Assert.Equal(1000, connection.ClosedWith);
    }
}
