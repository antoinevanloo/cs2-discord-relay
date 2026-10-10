using System.Collections.Concurrent;
using System.Threading.Channels;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Discord.Gateway;

namespace Cs2DiscordRelay.Core.Tests.Fakes;

/// <summary>Runs a <see cref="GatewayClient"/> against scripted connections and manual delays.</summary>
internal sealed class GatewayHarness : IAsyncDisposable
{
    public static readonly Uri GatewayUrl = new("wss://gateway.test");

    private readonly Channel<FakeGatewayTransport> _connections = Channel.CreateUnbounded<FakeGatewayTransport>();
    private readonly CancellationTokenSource _stop = new();
    private readonly Queue<Func<FakeGatewayTransport>> _scripted = new();

    public GatewayHarness(double random = 0.5)
    {
        Assert.True(BotToken.TryParse(TestData.BotToken, out var token, out _));
        Client = new GatewayClient(
            token,
            GatewayUrl,
            Messages.Enqueue,
            Logger,
            () =>
            {
                var transport = _scripted.TryDequeue(out var factory) ? factory() : new FakeGatewayTransport();
                _connections.Writer.TryWrite(transport);
                return transport;
            },
            Delays.Delay,
            () => random);
    }

    public GatewayClient Client { get; }

    public ManualDelays Delays { get; } = new();

    public ListLogger Logger { get; } = new();

    public ConcurrentQueue<GatewayMessage> Messages { get; } = new();

    public Task? Run { get; private set; }

    /// <summary>Makes the next connection attempt use <paramref name="factory"/>.</summary>
    public void Script(Func<FakeGatewayTransport> factory) => _scripted.Enqueue(factory);

    public void Start() => Run = Task.Run(() => Client.RunAsync(_stop.Token));

    /// <summary>Next connection attempt, once the client called <c>ConnectAsync</c> on it.</summary>
    public async Task<FakeGatewayTransport> NextConnectionAsync()
    {
        var connection = await _connections.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => connection.ConnectedTo is not null);
        return connection;
    }

    /// <summary>
    /// Waits until the client processed every frame pushed so far on <paramref name="connection"/>:
    /// frames are handled in order, so a marker message reaching the callback proves it.
    /// </summary>
    public async Task SyncAsync(FakeGatewayTransport connection)
    {
        var marker = "sync-" + Guid.NewGuid().ToString("N");
        connection.Push("{\"op\":0,\"s\":null,\"t\":\"MESSAGE_CREATE\",\"d\":{\"id\":\"" + marker + "\",\"channel_id\":\"0\",\"type\":0,\"author\":{\"id\":\"0\"}}}");
        await WaitUntilAsync(() => Messages.Any(m => m.Id == marker));
    }

    /// <summary>Starts the client and brings the first connection to READY.</summary>
    public async Task<FakeGatewayTransport> StartReadyAsync(int heartbeatIntervalMs = 45_000)
    {
        Start();
        var connection = await NextConnectionAsync();
        connection.Hello(heartbeatIntervalMs);
        await connection.NextSentAsync(2);
        connection.Ready();
        await WaitUntilAsync(() => Client.State == GatewayState.Connected);
        return connection;
    }

    public async Task StopAsync()
    {
        await _stop.CancelAsync();
        if (Run is not null)
        {
            await Run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Condition not met within 5 s.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (Run is not null)
        {
            await Run.WaitAsync(TimeSpan.FromSeconds(5));
        }

        _stop.Dispose();
    }
}
