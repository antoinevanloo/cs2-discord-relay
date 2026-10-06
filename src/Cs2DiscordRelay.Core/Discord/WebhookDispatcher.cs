using System.Net;
using System.Text;
using System.Threading.Channels;
using Cs2DiscordRelay.Core.Formatting;
using Cs2DiscordRelay.Core.Options;
using Microsoft.Extensions.Logging;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>
/// Sends messages to a Discord webhook from a background loop.
/// </summary>
/// <remarks>
/// <para><see cref="TryEnqueue"/> never blocks and never throws, so it is safe to call from the game thread.</para>
/// <para>
/// The loop groups bursts, honours Discord's rate limits (429 + <c>retry_after</c>, exhausted buckets),
/// retries transient failures with exponential backoff and stops for good when Discord says the
/// webhook does not exist anymore.
/// </para>
/// </remarks>
public sealed class WebhookDispatcher : IAsyncDisposable
{
    /// <summary>Attempts per message for network errors and 5xx responses.</summary>
    public const int MaxTransientAttempts = 3;

    /// <summary>Consecutive 429 responses tolerated for a single message.</summary>
    public const int MaxRateLimitAttempts = 5;

    /// <summary>Maximum number of messages read from the queue per cycle.</summary>
    public const int MaxMessagesPerCycle = 50;

    /// <summary>Minimum interval between two "queue full" warnings.</summary>
    public static readonly TimeSpan DroppedReportInterval = TimeSpan.FromSeconds(30);

    private readonly WebhookUrl _url;
    private readonly RelayOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Channel<FormattedMessage> _queue;
    private readonly CancellationTokenSource _stopping = new();

    private Task? _loop;
    private long _dropped;
    private DateTimeOffset _lastDroppedReport = DateTimeOffset.MinValue;
    private DateTimeOffset _bucketResetsAt = DateTimeOffset.MinValue;
    private volatile bool _disabled;
    private int _disposed;

    /// <summary>Creates a dispatcher. Call <see cref="Start"/> to begin sending.</summary>
    /// <param name="url">Validated webhook URL.</param>
    /// <param name="options">Normalized options.</param>
    /// <param name="http">Shared HTTP client, owned by the caller.</param>
    /// <param name="logger">Logger. Never receives the webhook URL.</param>
    /// <param name="timeProvider">Clock, <see cref="TimeProvider.System"/> by default.</param>
    public WebhookDispatcher(WebhookUrl url, RelayOptions options, HttpClient http, ILogger logger, TimeProvider? timeProvider = null)
        : this(url, options, http, logger, timeProvider, delay: null)
    {
    }

    /// <summary>Test seam: lets tests observe and skip delays deterministically.</summary>
    internal WebhookDispatcher(
        WebhookUrl url,
        RelayOptions options,
        HttpClient http,
        ILogger logger,
        TimeProvider? timeProvider,
        Func<TimeSpan, CancellationToken, Task>? delay)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _url = url;
        _options = options;
        _http = http;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _delay = delay ?? ((duration, token) => Task.Delay(duration, _time, token));
        _queue = Channel.CreateBounded<FormattedMessage>(
            new BoundedChannelOptions(options.MaxQueueSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            },
            _ => Interlocked.Increment(ref _dropped));
    }

    /// <summary><see langword="false"/> once Discord rejected the webhook (deleted or invalid).</summary>
    public bool IsEnabled => !_disabled;

    /// <summary>Starts the background send loop. Calling it twice has no effect.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _loop ??= Task.Run(() => RunAsync(_stopping.Token));
    }

    /// <summary>Queues a message without blocking. The oldest message is dropped when the queue is full.</summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="false"/> when the dispatcher is stopped or disabled.</returns>
    public bool TryEnqueue(FormattedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return !_disabled && _queue.Writer.TryWrite(message);
    }

    /// <summary>
    /// Stops accepting messages, lets the loop flush what is queued for up to
    /// <paramref name="flushTimeout"/>, then cancels it.
    /// </summary>
    /// <param name="flushTimeout">Maximum time spent sending queued messages.</param>
    /// <returns>A task completing once the loop has stopped.</returns>
    public async Task StopAsync(TimeSpan flushTimeout)
    {
        _queue.Writer.TryComplete();
        if (_loop is null)
        {
            return;
        }

        try
        {
            await _loop.WaitAsync(flushTimeout, _time).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
            await _loop.ConfigureAwait(false);
        }

        if (_queue.Reader.Count > 0)
        {
            Log.UnsentOnShutdown(_logger, _queue.Reader.Count);
        }
    }

    /// <summary>Stops the loop with a short flush window (see <see cref="StopAsync"/>).</summary>
    /// <returns>A task completing once the loop has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
        _stopping.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var batch = new List<FormattedMessage>(MaxMessagesPerCycle);
        try
        {
            while (await _queue.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_options.BatchWindow > TimeSpan.Zero)
                {
                    // Give a burst (several players chatting, a round of connections) the chance
                    // to arrive, so it goes out as one request instead of hitting the rate limit.
                    await _delay(_options.BatchWindow, cancellationToken).ConfigureAwait(false);
                }

                batch.Clear();
                while (batch.Count < MaxMessagesPerCycle && _queue.Reader.TryRead(out var message))
                {
                    batch.Add(message);
                }

                ReportDroppedMessages();

                foreach (var payload in PayloadBuilder.Build(batch, _options))
                {
                    if (!await SendAsync(payload, cancellationToken).ConfigureAwait(false))
                    {
                        _queue.Writer.TryComplete();
                        while (_queue.Reader.TryRead(out _))
                        {
                        }

                        return;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown.
        }
#pragma warning disable CA1031 // A bug in the relay must never take the game server down.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _disabled = true;
            _queue.Writer.TryComplete();
            Log.UnexpectedError(_logger, ex.GetType().Name, _url.Redact(ex.Message));
        }
    }

    /// <summary>Sends one payload, retrying as needed.</summary>
    /// <returns><see langword="false"/> when the relay must stop (webhook rejected).</returns>
    private async Task<bool> SendAsync(WebhookPayload payload, CancellationToken cancellationToken)
    {
        var json = WebhookJson.Serialize(payload);
        var transientAttempts = 0;
        var rateLimitAttempts = 0;

        while (true)
        {
            await WaitForBucketAsync(cancellationToken).ConfigureAwait(false);

            HttpResponseMessage response;
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await _http.PostAsync(_url.Uri, content, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken))
            {
                if (!await BackOffAsync(++transientAttempts, ex.GetType().Name + ": " + _url.Redact(ex.Message), cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }

                continue;
            }

            using (response)
            {
                if (RateLimitPolicy.GetBucketWait(response.Headers) is { } bucketWait)
                {
                    _bucketResetsAt = _time.GetUtcNow() + bucketWait;
                }

                var status = (int)response.StatusCode;
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    if (++rateLimitAttempts >= MaxRateLimitAttempts)
                    {
                        Log.RateLimitGaveUp(_logger, rateLimitAttempts);
                        return true;
                    }

                    var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                    var retryAfter = RateLimitPolicy.GetRetryAfter(response.Headers, body, _time);
                    Log.RateLimited(_logger, retryAfter.TotalMilliseconds);
                    await _delay(retryAfter, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
                {
                    _disabled = true;
                    Log.WebhookRejected(_logger, status);
                    return false;
                }

                if (status >= 500)
                {
                    if (!await BackOffAsync(++transientAttempts, $"HTTP {status}", cancellationToken).ConfigureAwait(false))
                    {
                        return true;
                    }

                    continue;
                }

                var errorBody = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                var discordMessage = WebhookJson.TryParseError(errorBody)?.Message ?? response.ReasonPhrase ?? "unknown error";
                Log.PayloadRejected(_logger, status, _url.Redact(discordMessage));
                return true;
            }
        }
    }

    /// <returns><see langword="false"/> when the message must be dropped.</returns>
    private async Task<bool> BackOffAsync(int attempt, string error, CancellationToken cancellationToken)
    {
        if (attempt >= MaxTransientAttempts)
        {
            Log.TransientGaveUp(_logger, attempt, error);
            return false;
        }

        var delay = RateLimitPolicy.GetBackoff(attempt);
        Log.TransientFailure(_logger, error, attempt, MaxTransientAttempts, delay.TotalMilliseconds);
        await _delay(delay, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task WaitForBucketAsync(CancellationToken cancellationToken)
    {
        var wait = _bucketResetsAt - _time.GetUtcNow();
        if (wait > TimeSpan.Zero)
        {
            await _delay(wait, cancellationToken).ConfigureAwait(false);
        }
    }

    private void ReportDroppedMessages()
    {
        var now = _time.GetUtcNow();
        if (Interlocked.Read(ref _dropped) == 0 || now - _lastDroppedReport < DroppedReportInterval)
        {
            return;
        }

        _lastDroppedReport = now;
        Log.MessagesDropped(_logger, Interlocked.Exchange(ref _dropped, 0));
    }

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested); // HttpClient timeout

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }
}
