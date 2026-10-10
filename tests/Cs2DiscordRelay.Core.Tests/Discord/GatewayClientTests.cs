using System.Net.WebSockets;
using System.Text.Json;
using Cs2DiscordRelay.Core.Discord.Gateway;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests.Discord;

public sealed class GatewayClientTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(45_000);

    [Fact]
    public async Task Identifies_with_the_message_intents_after_hello()
    {
        await using var h = new GatewayHarness();
        h.Start();
        var connection = await h.NextConnectionAsync();

        connection.Hello();
        var identify = await connection.NextSentAsync();

        Assert.Equal("wss://gateway.test/?v=10&encoding=json", connection.ConnectedTo!.AbsoluteUri);
        Assert.Equal(2, identify.GetProperty("op").GetInt32());
        var data = identify.GetProperty("d");
        Assert.Equal(TestData.BotToken, data.GetProperty("token").GetString());
        Assert.Equal(33281, data.GetProperty("intents").GetInt32());
        Assert.Equal("cs2-discord-relay", data.GetProperty("properties").GetProperty("browser").GetString());
    }

    [Fact]
    public async Task Ready_connects_and_exposes_the_bot_user()
    {
        await using var h = new GatewayHarness();
        await h.StartReadyAsync();

        Assert.Equal(GatewayState.Connected, h.Client.State);
        Assert.Equal("RelayBot", h.Client.CurrentUser!.Username);
        Assert.Contains("Discord gateway connected as RelayBot.", h.Logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task First_heartbeat_is_jittered_then_sent_every_interval_with_the_last_sequence()
    {
        await using var h = new GatewayHarness(random: 0.25);
        var connection = await h.StartReadyAsync();
        connection.Dispatch("MESSAGE_CREATE", 7, """{"id":"9","channel_id":"555","type":0,"content":"x","author":{"id":"7"}}""");
        await GatewayHarness.WaitUntilAsync(() => !h.Messages.IsEmpty); // sequence 7 processed

        await h.Delays.ElapseAsync(Interval * 0.25);
        var first = await connection.NextSentAsync(1);
        Assert.Equal(7, first.GetProperty("d").GetInt32());

        connection.Push("""{"op":11,"d":null,"s":null,"t":null}""");
        await h.SyncAsync(connection);
        await h.Delays.ElapseAsync(Interval);
        Assert.Equal(7, (await connection.NextSentAsync(1)).GetProperty("d").GetInt32());
    }

    [Fact]
    public async Task Heartbeat_before_any_dispatch_sends_null()
    {
        await using var h = new GatewayHarness();
        h.Start();
        var connection = await h.NextConnectionAsync();
        connection.Hello();
        await connection.NextSentAsync(2);

        await h.Delays.ElapseAsync(Interval * 0.5);

        Assert.Equal(JsonValueKind.Null, (await connection.NextSentAsync(1)).GetProperty("d").ValueKind);
    }

    [Fact]
    public async Task Answers_a_heartbeat_request_immediately()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        connection.Push("""{"op":1,"d":null,"s":null,"t":null}""");

        Assert.Equal(1, (await connection.NextSentAsync(1)).GetProperty("d").GetInt32());
    }

    [Fact]
    public async Task Missing_heartbeat_ack_closes_as_resumable_and_resumes_on_the_resume_url()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        await h.Delays.ElapseAsync(Interval * 0.5);
        await connection.NextSentAsync(1); // never acknowledged
        await h.Delays.ElapseAsync(Interval);

        await connection.Closed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(4000, connection.ClosedWith);

        var second = await h.NextConnectionAsync();
        Assert.Equal("wss://resume.test/?v=10&encoding=json", second.ConnectedTo!.AbsoluteUri);
        second.Hello();
        var resume = await second.NextSentAsync();
        Assert.Equal(6, resume.GetProperty("op").GetInt32());
        Assert.Equal("session-1", resume.GetProperty("d").GetProperty("session_id").GetString());
        Assert.Equal(1, resume.GetProperty("d").GetProperty("seq").GetInt32());

        second.Dispatch("RESUMED", 2, "{}");
        await GatewayHarness.WaitUntilAsync(() => h.Client.State == GatewayState.Connected);
    }

    [Fact]
    public async Task Reconnect_request_resumes_without_waiting()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        connection.Push("""{"op":7,"d":null,"s":null,"t":null}""");

        var second = await h.NextConnectionAsync();
        Assert.Equal("wss://resume.test/?v=10&encoding=json", second.ConnectedTo!.AbsoluteUri);
        Assert.Equal(4000, connection.ClosedWith);
        Assert.DoesNotContain(h.Delays.Requested, d => d >= TimeSpan.FromSeconds(1) && d < Interval * 0.5);
    }

    [Fact]
    public async Task Non_resumable_invalid_session_waits_then_identifies_on_the_gateway_url()
    {
        await using var h = new GatewayHarness(random: 0.5);
        var connection = await h.StartReadyAsync();

        connection.Push("""{"op":9,"d":false,"s":null,"t":null}""");
        await h.Delays.ElapseAsync(TimeSpan.FromSeconds(3)); // 1 s + 4 s * random

        var second = await h.NextConnectionAsync();
        Assert.Equal("wss://gateway.test/?v=10&encoding=json", second.ConnectedTo!.AbsoluteUri);
        second.Hello();
        Assert.Equal(2, (await second.NextSentAsync()).GetProperty("op").GetInt32());
    }

    [Fact]
    public async Task Resumable_invalid_session_resumes()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        connection.Push("""{"op":9,"d":true,"s":null,"t":null}""");

        var second = await h.NextConnectionAsync();
        second.Hello();
        Assert.Equal(6, (await second.NextSentAsync()).GetProperty("op").GetInt32());
    }

    [Theory]
    [InlineData(4007)]
    [InlineData(4009)]
    public async Task Expired_sessions_start_over(int closeCode)
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        connection.ServerClose(closeCode);

        var second = await h.NextConnectionAsync();
        Assert.Equal("wss://gateway.test/?v=10&encoding=json", second.ConnectedTo!.AbsoluteUri);
        second.Hello();
        Assert.Equal(2, (await second.NextSentAsync()).GetProperty("op").GetInt32());
    }

    [Fact]
    public async Task Dropped_connection_resumes()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        connection.DropConnection();

        var second = await h.NextConnectionAsync();
        second.Hello();
        Assert.Equal(6, (await second.NextSentAsync()).GetProperty("op").GetInt32());
    }

    [Theory]
    [InlineData(4004, "Discord rejected the bot token (close code 4004)")]
    [InlineData(4014, "Message Content Intent is not enabled for this bot (close code 4014)")]
    [InlineData(4013, "Discord rejected the requested intents (close code 4013)")]
    public async Task Fatal_close_codes_stop_for_good(int closeCode, string expected)
    {
        await using var h = new GatewayHarness();
        h.Start();
        var connection = await h.NextConnectionAsync();
        connection.Hello();
        await connection.NextSentAsync(2);

        connection.ServerClose(closeCode);
        await h.Run!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(GatewayState.Stopped, h.Client.State);
        Assert.StartsWith(expected, h.Client.LastError, StringComparison.Ordinal);
        Assert.Contains(h.Logger.Entries, e => e.Message.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Failed_connections_back_off_exponentially()
    {
        await using var h = new GatewayHarness(random: 1);
        for (var i = 0; i < 3; i++)
        {
            h.Script(() => new FakeGatewayTransport { ConnectError = new WebSocketException("unreachable") });
        }

        h.Start();
        await h.NextConnectionAsync();
        await h.Delays.ElapseAsync(TimeSpan.FromSeconds(1));
        await h.NextConnectionAsync();
        await h.Delays.ElapseAsync(TimeSpan.FromSeconds(2));
        await h.NextConnectionAsync();
        await h.Delays.ElapseAsync(TimeSpan.FromSeconds(4));
        await h.NextConnectionAsync();

        Assert.Equal(GatewayState.Reconnecting, h.Client.State);
        Assert.Contains("unreachable", h.Client.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Connection_timeouts_are_retried()
    {
        await using var h = new GatewayHarness(random: 1);
        h.Script(() => new FakeGatewayTransport { ConnectError = new OperationCanceledException("connect timed out") });

        h.Start();
        await h.NextConnectionAsync();
        await h.Delays.ElapseAsync(TimeSpan.FromSeconds(1));
        await h.NextConnectionAsync();

        Assert.Equal(GatewayState.Reconnecting, h.Client.State);
    }

    [Fact]
    public void Backoff_is_capped()
    {
        var client = new GatewayHarness(random: 1).Client;

        Assert.Equal(TimeSpan.FromSeconds(60), client.GetBackoff(30));
        Assert.Equal(TimeSpan.FromSeconds(1), client.GetBackoff(1));
    }

    [Fact]
    public async Task Shutdown_closes_normally_so_the_session_ends()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        await h.StopAsync();

        Assert.Equal(1000, connection.ClosedWith);
        Assert.Equal(GatewayState.Stopped, h.Client.State);
    }

    [Fact]
    public async Task Message_create_is_handed_to_the_callback()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        connection.Dispatch("MESSAGE_CREATE", 2, """
            {"id":"9","channel_id":"555","type":0,"content":"hello <@42>","author":{"id":"7","username":"nova","global_name":"Nova"},
             "member":{"nick":"Nova CT","roles":["11"]},"mentions":[{"id":"42","username":"RelayBot"}],"attachments":[]}
            """);

        await GatewayHarness.WaitUntilAsync(() => !h.Messages.IsEmpty);
        var message = Assert.Single(h.Messages);
        Assert.Equal("555", message.ChannelId);
        Assert.Equal("hello <@42>", message.Content);
        Assert.Equal("Nova CT", message.Member!.Nick);
        Assert.Equal("RelayBot", Assert.Single(message.Mentions!).Username);
    }

    [Fact]
    public async Task Malformed_frames_are_ignored()
    {
        await using var h = new GatewayHarness();
        var connection = await h.StartReadyAsync();

        connection.Push("not json");
        connection.Dispatch("MESSAGE_CREATE", 2, """{"id":"9","channel_id":"555","type":0,"content":"still alive","author":{"id":"7"}}""");

        await GatewayHarness.WaitUntilAsync(() => !h.Messages.IsEmpty);
        Assert.Equal(GatewayState.Connected, h.Client.State);
    }

    [Fact]
    public async Task Never_logs_the_token()
    {
        await using var h = new GatewayHarness();
        h.Script(() => new FakeGatewayTransport { ConnectError = new WebSocketException($"refused for {TestData.BotToken}") });
        h.Start();
        await h.NextConnectionAsync();
        await h.Delays.NextAsync();

        Assert.DoesNotContain(TestData.BotToken, h.Logger.AllText, StringComparison.Ordinal);
        Assert.DoesNotContain(TestData.BotToken, h.Client.LastError, StringComparison.Ordinal);
    }
}
