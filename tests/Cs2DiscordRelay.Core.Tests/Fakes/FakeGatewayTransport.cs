using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Cs2DiscordRelay.Core.Discord.Gateway;

namespace Cs2DiscordRelay.Core.Tests.Fakes;

/// <summary>A scripted gateway connection: tests push server frames and await client frames.</summary>
internal sealed class FakeGatewayTransport : IGatewayTransport
{
    private const string Drop = "\0drop";

    private readonly Channel<string?> _incoming = Channel.CreateUnbounded<string?>();
    private readonly Channel<JsonElement> _sent = Channel.CreateUnbounded<JsonElement>();

    public Uri? ConnectedTo { get; private set; }

    public Exception? ConnectError { get; init; }

    public int? CloseStatus { get; private set; }

    /// <summary>Close code sent by the client, if it closed the connection.</summary>
    public int? ClosedWith { get; private set; }

    public TaskCompletionSource Closed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        ConnectedTo = uri;
        return ConnectError is null ? Task.CompletedTask : Task.FromException(ConnectError);
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var frame = await _incoming.Reader.ReadAsync(cancellationToken);
        return frame == Drop ? throw new WebSocketException("connection reset") : frame;
    }

    public Task SendAsync(string message, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(message);
        _sent.Writer.TryWrite(json.RootElement.Clone());
        return Task.CompletedTask;
    }

    public Task CloseAsync(int closeCode, CancellationToken cancellationToken)
    {
        ClosedWith ??= closeCode;
        Closed.TrySetResult();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Push(string json) => _incoming.Writer.TryWrite(json);

    public void Hello(int heartbeatIntervalMs = 45_000) => Push($$"""{"op":10,"d":{"heartbeat_interval":{{heartbeatIntervalMs}}},"s":null,"t":null}""");

    public void Ready(int sequence = 1, string sessionId = "session-1", string resumeUrl = "wss://resume.test") =>
        Dispatch("READY", sequence, $$$"""{"v":10,"session_id":"{{{sessionId}}}","resume_gateway_url":"{{{resumeUrl}}}","user":{"id":"42","username":"RelayBot","bot":true}}""");

    public void Dispatch(string eventName, int sequence, string data) =>
        Push($$"""{"op":0,"s":{{sequence}},"t":"{{eventName}}","d":{{data}}}""");

    public void ServerClose(int? code)
    {
        CloseStatus = code;
        _incoming.Writer.TryWrite(null);
    }

    public void DropConnection() => _incoming.Writer.TryWrite(Drop);

    /// <summary>Next frame sent by the client.</summary>
    public async Task<JsonElement> NextSentAsync() =>
        await _sent.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

    /// <summary>Next frame sent by the client with opcode <paramref name="op"/>, skipping the others.</summary>
    public async Task<JsonElement> NextSentAsync(int op)
    {
        while (true)
        {
            var frame = await NextSentAsync();
            if (frame.GetProperty("op").GetInt32() == op)
            {
                return frame;
            }
        }
    }

    public bool HasSent => _sent.Reader.Count > 0;
}
