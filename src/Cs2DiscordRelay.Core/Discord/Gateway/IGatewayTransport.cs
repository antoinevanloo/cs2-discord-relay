namespace Cs2DiscordRelay.Core.Discord.Gateway;

/// <summary>A text WebSocket connection to the gateway. One instance per connection attempt.</summary>
internal interface IGatewayTransport : IAsyncDisposable
{
    /// <summary>Close code sent by the server, once <see cref="ReceiveAsync"/> returned <see langword="null"/>.</summary>
    int? CloseStatus { get; }

    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>Next text message, or <see langword="null"/> once the server closed the connection.</summary>
    Task<string?> ReceiveAsync(CancellationToken cancellationToken);

    /// <summary>Sends a text message. Safe to call concurrently with <see cref="ReceiveAsync"/> and other sends.</summary>
    Task SendAsync(string message, CancellationToken cancellationToken);

    /// <summary>Starts a close handshake with <paramref name="closeCode"/>. Never throws.</summary>
    Task CloseAsync(int closeCode, CancellationToken cancellationToken);
}
