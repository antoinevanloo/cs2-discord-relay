using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Events;
using Cs2DiscordRelay.Core.Formatting;
using Cs2DiscordRelay.Core.Options;
using Microsoft.Extensions.Logging;

namespace Cs2DiscordRelay.Core;

/// <summary>
/// Entry point used by the game plugin: filters, formats and queues events.
/// Owns the HTTP client and the dispatcher.
/// </summary>
public sealed class RelayService : IAsyncDisposable
{
    private readonly RelayOptions _options;
    private readonly MessageFormatter _formatter;
    private readonly HttpClient _http;
    private readonly WebhookDispatcher _dispatcher;

    /// <summary>Creates and starts the relay.</summary>
    /// <param name="url">Validated webhook URL.</param>
    /// <param name="options">Normalized options.</param>
    /// <param name="logger">Logger. Never receives the webhook URL.</param>
    public RelayService(WebhookUrl url, RelayOptions options, ILogger logger)
        : this(url, options, logger, DiscordHttp.CreateClient(), timeProvider: null)
    {
    }

    internal RelayService(WebhookUrl url, RelayOptions options, ILogger logger, HttpClient http, TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _formatter = new MessageFormatter(options);
        _http = http;
        _dispatcher = new WebhookDispatcher(url, options, http, logger, timeProvider);
        _dispatcher.Start();
    }

    /// <summary><see langword="false"/> once Discord rejected the webhook.</summary>
    public bool IsEnabled => _dispatcher.IsEnabled;

    /// <summary>
    /// Filters, formats and queues an event. Never blocks: safe to call from the game thread.
    /// </summary>
    /// <param name="relayEvent">The event.</param>
    /// <returns><see langword="true"/> when the event was queued for Discord.</returns>
    public bool Publish(RelayEvent relayEvent)
    {
        ArgumentNullException.ThrowIfNull(relayEvent);

        return EventFilter.ShouldRelay(relayEvent, _options)
            && _dispatcher.TryEnqueue(_formatter.Format(relayEvent));
    }

    /// <summary>Flushes pending messages for a few seconds, then releases the HTTP client.</summary>
    /// <returns>A task completing once everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        await _dispatcher.DisposeAsync().ConfigureAwait(false);
        _http.Dispose();
    }
}
