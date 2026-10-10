using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace Cs2DiscordRelay.Core.Discord.Gateway;

/// <summary><see cref="IGatewayTransport"/> over <see cref="ClientWebSocket"/>, JSON text frames, no compression.</summary>
internal sealed class WebSocketGatewayTransport : IGatewayTransport
{
    /// <summary>Guards against a misbehaving server: gateway events we care about are a few kilobytes.</summary>
    public const int MaxMessageBytes = 4 * 1024 * 1024;

    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketGatewayTransport() => _socket.Options.SetRequestHeader("User-Agent", DiscordHttp.UserAgent);

    public int? CloseStatus => (int?)_socket.CloseStatus;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => _socket.ConnectAsync(uri, cancellationToken);

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            var message = new ArrayBufferWriter<byte>();
            while (true)
            {
                var result = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (message.WrittenCount + result.Count > MaxMessageBytes)
                {
                    throw new WebSocketException(WebSocketError.Faulted, "Gateway message too large.");
                }

                message.Write(buffer.AsSpan(0, result.Count));
                if (result.EndOfMessage)
                {
                    // Binary frames only carry compressed data, which the bridge never requests.
                    return result.MessageType == WebSocketMessageType.Text ? Encoding.UTF8.GetString(message.WrittenSpan) : string.Empty;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task CloseAsync(int closeCode, CancellationToken cancellationToken)
    {
        if (_socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await _socket.CloseOutputAsync((WebSocketCloseStatus)closeCode, statusDescription: null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // Best effort: the connection is going away anyway.
        }
    }

    public ValueTask DisposeAsync()
    {
        _socket.Dispose();
        _sendLock.Dispose();
        return ValueTask.CompletedTask;
    }
}
