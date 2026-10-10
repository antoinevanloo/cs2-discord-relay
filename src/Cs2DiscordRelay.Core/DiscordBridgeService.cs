using System.Globalization;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Discord.Gateway;
using Cs2DiscordRelay.Core.Inbound;
using Microsoft.Extensions.Logging;

namespace Cs2DiscordRelay.Core;

/// <summary>
/// Discord → game relay: reads one Discord channel through the gateway and queues its messages, converted to
/// plain text, for the game thread. Owns its HTTP client and gateway connection.
/// </summary>
/// <remarks>Everything runs in the background. <see cref="Drain"/> is the only method meant for the game thread, and it never blocks.</remarks>
public sealed class DiscordBridgeService : IAsyncDisposable
{
    /// <summary>Used when <c>GET /gateway/bot</c> does not return a URL.</summary>
    public static readonly Uri DefaultGatewayUrl = new("wss://gateway.discord.gg");

    private static readonly TimeSpan RateLimitReportInterval = TimeSpan.FromSeconds(30);

    private readonly BotToken _token;
    private readonly InboundOptions _options;
    private readonly WebhookUrl? _webhook;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly DiscordRestClient _rest;
    private readonly Func<IGatewayTransport>? _transportFactory;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<double>? _random;
    private readonly TimeProvider _time;
    private readonly InboundRateLimiter _rateLimiter;
    private readonly InboundQueue _queue = new();
    private readonly CancellationTokenSource _stopping = new();

    private Task? _run;
    private GatewayClient? _gateway;
    private ulong _channelId;
    private string _channelName = "?";
    private DateTimeOffset _lastRateLimitReport = DateTimeOffset.MinValue;
    private volatile string? _stopReason;
    private int _disposed;

    /// <summary>Creates the bridge. Call <see cref="Start"/> to connect.</summary>
    /// <param name="token">Validated bot token.</param>
    /// <param name="options">Normalized options.</param>
    /// <param name="webhook">The relay's webhook: its channel is read when <see cref="InboundOptions.ChannelId"/> is not set.</param>
    /// <param name="logger">Logger. Never receives the token or the webhook URL.</param>
    public DiscordBridgeService(BotToken token, InboundOptions options, WebhookUrl? webhook, ILogger logger)
        : this(token, options, webhook, logger, DiscordHttp.CreateClient(), transportFactory: null, delay: null, random: null, timeProvider: null)
    {
    }

    internal DiscordBridgeService(
        BotToken token,
        InboundOptions options,
        WebhookUrl? webhook,
        ILogger logger,
        HttpClient http,
        Func<IGatewayTransport>? transportFactory,
        Func<TimeSpan, CancellationToken, Task>? delay,
        Func<double>? random,
        TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _token = token;
        _options = options;
        _webhook = webhook;
        _logger = logger;
        _http = http;
        _time = timeProvider ?? TimeProvider.System;
        _delay = delay ?? ((duration, cancellationToken) => Task.Delay(duration, _time, cancellationToken));
        _random = random;
        _transportFactory = transportFactory;
        _rest = new DiscordRestClient(http, token, _time, _delay);
        _rateLimiter = new InboundRateLimiter(options);
    }

    /// <summary>Lines waiting for the game thread.</summary>
    public int PendingCount => _queue.Count;

    /// <summary><see langword="true"/> once the bridge stopped for good (configuration error or shutdown).</summary>
    public bool IsStopped => _stopReason is not null || _gateway?.State == GatewayState.Stopped;

    /// <summary>Connects in the background. Calling it twice has no effect.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _run ??= Task.Run(() => RunAsync(_stopping.Token));
    }

    /// <summary>
    /// Moves the lines received since the last call to <paramref name="into"/>. Lines older than
    /// <see cref="InboundOptions.MaxAge"/> are discarded. Never blocks: call it from the game thread.
    /// </summary>
    /// <param name="into">Receives the lines, oldest first.</param>
    /// <returns>The number of lines added.</returns>
    public int Drain(ICollection<GameChatLine> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        var before = into.Count;
        _queue.Drain(_time.GetUtcNow(), InboundOptions.MaxAge, into);
        return into.Count - before;
    }

    /// <summary>One-line, human readable state, for admin commands. Never contains the token.</summary>
    /// <returns>The status line.</returns>
    public string DescribeStatus()
    {
        if (_stopReason is { } reason)
        {
            return "Stopped: " + reason;
        }

        var gateway = _gateway;
        return gateway?.State switch
        {
            null => "Starting.",
            GatewayState.Connecting => "Connecting to the Discord gateway.",
            GatewayState.Connected => string.Create(
                CultureInfo.InvariantCulture,
                $"Connected as {gateway.CurrentUser?.Username ?? "?"}, reading #{_channelName}{(_options.AllowedRoleIds.Count > 0 ? $" (restricted to {_options.AllowedRoleIds.Count} role(s))" : string.Empty)}."),
            GatewayState.Reconnecting => $"Reconnecting ({gateway.LastError}).",
            _ => "Stopped: " + (gateway.LastError ?? "shutdown."),
        };
    }

    /// <summary>Disconnects (ending the gateway session) and releases the HTTP client.</summary>
    /// <returns>A task completing once everything is released.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_run is not null)
        {
            try
            {
                await _run.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // The socket is torn down with the HTTP client below.
            }
        }

        _stopping.Dispose();
        _http.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var user = await CallAsync(_rest.GetCurrentUserAsync, cancellationToken).ConfigureAwait(false);
            if (!user.IsSuccess)
            {
                Stop(user.StatusCode == 401
                    ? "Discord rejected DiscordToGame.BotToken (HTTP 401). Copy it again from Developer Portal > Bot > Reset Token."
                    : $"Could not validate the bot token ({user.Error}).");
                return;
            }

            if (await ResolveChannelAsync(cancellationToken).ConfigureAwait(false) is not { } channelId)
            {
                return;
            }

            var channel = await CallAsync(ct => _rest.GetChannelAsync(channelId, ct), cancellationToken).ConfigureAwait(false);
            if (!channel.IsSuccess)
            {
                Stop(string.Create(
                    CultureInfo.InvariantCulture,
                    $"The bot cannot see channel {channelId} ({channel.Error}). Invite it to the server and allow it to View Channel there."));
                return;
            }

            _channelId = channelId;
            _channelName = channel.Value!.Name ?? channelId.ToString(CultureInfo.InvariantCulture);

            var gatewayBot = await CallAsync(_rest.GetGatewayBotAsync, cancellationToken).ConfigureAwait(false);
            var gatewayUrl = Uri.TryCreate(gatewayBot.Value?.Url, UriKind.Absolute, out var url) ? url : DefaultGatewayUrl;

            _gateway = new GatewayClient(_token, gatewayUrl, OnMessage, _logger, _transportFactory, _delay, _random);
            Log.BridgeReading(_logger, _channelName, user.Value!.Username ?? "?");
            await _gateway.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown.
        }
#pragma warning disable CA1031 // A bug in the bridge must never take the game server down.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Stop($"unexpected error ({ex.GetType().Name}: {_token.Redact(_webhook?.Redact(ex.Message) ?? ex.Message)}).");
        }
    }

    private async Task<ulong?> ResolveChannelAsync(CancellationToken cancellationToken)
    {
        if (_options.ChannelId is { } configured)
        {
            return configured;
        }

        if (_webhook is null)
        {
            Stop("Set DiscordToGame.ChannelId, or a valid WebhookUrl to read its channel.");
            return null;
        }

        if (_webhook.ThreadId is { } threadId)
        {
            return threadId;
        }

        var info = await CallAsync(ct => _rest.GetWebhookAsync(_webhook, ct), cancellationToken).ConfigureAwait(false);
        if (Snowflake.TryParse(info.Value?.ChannelId, out var channelId))
        {
            return channelId;
        }

        Stop($"Could not find the webhook's channel ({info.Error ?? "no channel"}). Set DiscordToGame.ChannelId.");
        return null;
    }

    /// <summary>Retries network errors and 5xx with a capped backoff; returns any other outcome.</summary>
    private async Task<DiscordRestResult<T>> CallAsync<T>(Func<CancellationToken, Task<DiscordRestResult<T>>> call, CancellationToken cancellationToken)
        where T : class
    {
        for (var attempt = 1; ; attempt++)
        {
            var result = await call(cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess || !result.IsTransient)
            {
                return result;
            }

            var wait = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Min(attempt - 1, 6)), GatewayClient.MaxBackoff.TotalSeconds));
            Log.BridgeRetrying(_logger, result.Error ?? "unknown error", wait.TotalMilliseconds);
            await _delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnMessage(GatewayMessage payload)
    {
        if (DiscordMessage.From(payload) is not { } message
            || !InboundFilter.ShouldRelay(message, _channelId, ParseId(_gateway?.CurrentUser?.Id), _options)
            || GameChatFormatter.Format(message, _options) is not { } line)
        {
            return;
        }

        var now = _time.GetUtcNow();
        if (_rateLimiter.TryAcquire(message.AuthorId, now))
        {
            _queue.Enqueue(line, now);
        }

        if (now - _lastRateLimitReport >= RateLimitReportInterval && _rateLimiter.TakeDropped() is > 0 and var dropped)
        {
            _lastRateLimitReport = now;
            Log.BridgeRateLimited(_logger, dropped);
        }
    }

    private void Stop(string reason)
    {
        _stopReason = reason;
        Log.GatewayStopped(_logger, reason);
    }

    private static ulong ParseId(string? id) => Snowflake.TryParse(id, out var value) ? value : 0;
}
