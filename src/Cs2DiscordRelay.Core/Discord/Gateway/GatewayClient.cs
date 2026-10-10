using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cs2DiscordRelay.Core.Discord.Gateway;

/// <summary>Connection state of the gateway client.</summary>
public enum GatewayState
{
    /// <summary>Opening the first connection.</summary>
    Connecting,

    /// <summary>Session ready: messages are received.</summary>
    Connected,

    /// <summary>Connection lost, waiting before the next attempt.</summary>
    Reconnecting,

    /// <summary>Stopped for good (fatal error or shutdown).</summary>
    Stopped,
}

/// <summary>
/// Minimal Discord gateway client: identify, heartbeat (with zombie detection), resume and reconnect.
/// It only listens to guild messages. See https://discord.com/developers/docs/events/gateway.
/// </summary>
/// <remarks>All the work happens in <see cref="RunAsync"/>, on a background thread. Never call game APIs from <c>onMessage</c>.</remarks>
internal sealed class GatewayClient
{
    /// <summary>GUILDS | GUILD_MESSAGES | MESSAGE_CONTENT.</summary>
    public const int Intents = (1 << 0) | (1 << 9) | (1 << 15);

    public const string ApiQuery = "v=10&encoding=json";

    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    private const long NoSequence = -1;

    private readonly BotToken _token;
    private readonly Uri _gatewayUrl;
    private readonly Action<GatewayMessage> _onMessage;
    private readonly ILogger _logger;
    private readonly Func<IGatewayTransport> _transportFactory;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Func<double> _random;

    private string? _sessionId;
    private Uri? _resumeUrl;
    private long _sequence = NoSequence;
    private volatile GatewayState _state = GatewayState.Connecting;

    public GatewayClient(
        BotToken token,
        Uri gatewayUrl,
        Action<GatewayMessage> onMessage,
        ILogger logger,
        Func<IGatewayTransport>? transportFactory = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<double>? random = null)
    {
        _token = token;
        _gatewayUrl = gatewayUrl;
        _onMessage = onMessage;
        _logger = logger;
        _transportFactory = transportFactory ?? (() => new WebSocketGatewayTransport());
        _delay = delay ?? Task.Delay;
        _random = random ?? Random.Shared.NextDouble;
    }

    public GatewayState State => _state;

    /// <summary>Why the client stopped or last disconnected. Never contains the token.</summary>
    public string? LastError { get; private set; }

    /// <summary>The bot user, once READY was received.</summary>
    public DiscordUser? CurrentUser { get; private set; }

    /// <summary>Connects and keeps the connection alive until <paramref name="cancellationToken"/> fires or a fatal error occurs.</summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var failures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var resume = _sessionId is not null && _resumeUrl is not null;
            var outcome = await RunSessionAsync(resume ? _resumeUrl! : _gatewayUrl, resume, cancellationToken).ConfigureAwait(false);

            switch (outcome.Action)
            {
                case SessionEnd.Shutdown:
                    _state = GatewayState.Stopped;
                    return;
                case SessionEnd.Fatal:
                    LastError = outcome.Reason;
                    _state = GatewayState.Stopped;
                    Log.GatewayStopped(_logger, outcome.Reason);
                    return;
                case SessionEnd.Reidentify:
                    ResetSession();
                    break;
            }

            LastError = outcome.Reason;
            _state = GatewayState.Reconnecting;
            failures = outcome.WasReady ? 0 : failures + 1;

            var wait = outcome.Action == SessionEnd.Reidentify && outcome.RandomWait
                ? TimeSpan.FromSeconds(1 + (4 * _random())) // after Invalid Session, before a new Identify
                : failures == 0 ? TimeSpan.Zero : GetBackoff(failures);
            Log.GatewayReconnecting(_logger, outcome.Reason, wait.TotalMilliseconds);

            if (wait > TimeSpan.Zero)
            {
                try
                {
                    await _delay(wait, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _state = GatewayState.Stopped;
    }

    /// <summary>1 s, 2 s, 4 s... up to <see cref="MaxBackoff"/>, scaled by a random factor in [0.5, 1].</summary>
    internal TimeSpan GetBackoff(int failures)
    {
        var seconds = Math.Min(Math.Pow(2, Math.Min(failures - 1, 10)), MaxBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(seconds * (0.5 + (_random() / 2)));
    }

    private async Task<SessionOutcome> RunSessionAsync(Uri baseUrl, bool resume, CancellationToken cancellationToken)
    {
        var transport = _transportFactory();
        await using (transport.ConfigureAwait(false))
        {
            using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var heartbeat = new HeartbeatState();
            Task? heartbeatLoop = null;
            var wasReady = false;

            try
            {
                using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(session.Token))
                {
                    // ClientWebSocket has no connect timeout of its own.
                    connectTimeout.CancelAfter(ConnectTimeout);
                    await transport.ConnectAsync(WithApiQuery(baseUrl), connectTimeout.Token).ConfigureAwait(false);
                }

                while (true)
                {
                    var text = await transport.ReceiveAsync(session.Token).ConfigureAwait(false);
                    if (text is null)
                    {
                        return Closed(transport.CloseStatus, wasReady);
                    }

                    if (!TryParse(text, out var payload))
                    {
                        continue;
                    }

                    if (payload.Sequence is { } sequence)
                    {
                        Interlocked.Exchange(ref _sequence, sequence);
                    }

                    switch (payload.Op)
                    {
                        case GatewayOpcodes.Hello:
                            var interval = TimeSpan.FromMilliseconds(Deserialize(payload, DiscordApiJsonContext.Default.HelloData)?.HeartbeatInterval ?? 41_250);
                            heartbeatLoop ??= HeartbeatLoopAsync(transport, interval, heartbeat, session);
                            await transport.SendAsync(resume ? ResumeJson() : IdentifyJson(), session.Token).ConfigureAwait(false);
                            break;

                        case GatewayOpcodes.HeartbeatAck:
                            heartbeat.Acknowledged = true;
                            break;

                        case GatewayOpcodes.Heartbeat:
                            await transport.SendAsync(HeartbeatJson(), session.Token).ConfigureAwait(false);
                            break;

                        case GatewayOpcodes.Reconnect:
                            await transport.CloseAsync(GatewayCloseCodes.Resumable, CancellationToken.None).ConfigureAwait(false);
                            return new(SessionEnd.Resume, "Discord asked to reconnect", wasReady);

                        case GatewayOpcodes.InvalidSession:
                            await transport.CloseAsync(GatewayCloseCodes.Resumable, CancellationToken.None).ConfigureAwait(false);
                            return Deserialize(payload, DiscordApiJsonContext.Default.Boolean)
                                ? new(SessionEnd.Resume, "session invalidated (resumable)", wasReady)
                                : new(SessionEnd.Reidentify, "session invalidated", wasReady, RandomWait: true);

                        case GatewayOpcodes.Dispatch:
                            wasReady |= HandleDispatch(payload);
                            break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await transport.CloseAsync(GatewayCloseCodes.NormalClosure, CancellationToken.None).ConfigureAwait(false);
                return new(SessionEnd.Shutdown, "shutdown", wasReady);
            }
            catch (OperationCanceledException) when (heartbeat.Zombie)
            {
                await transport.CloseAsync(GatewayCloseCodes.Resumable, CancellationToken.None).ConfigureAwait(false);
                return new(SessionEnd.Resume, "no heartbeat acknowledgement", wasReady);
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException or OperationCanceledException)
            {
                // Dropped without a close code: the session can be resumed.
                return new(SessionEnd.Resume, _token.Redact(ex.GetType().Name + ": " + ex.Message), wasReady);
            }
            finally
            {
                await session.CancelAsync().ConfigureAwait(false);
                if (heartbeatLoop is not null)
                {
                    await heartbeatLoop.ConfigureAwait(false);
                }
            }
        }
    }

    /// <returns><see langword="true"/> when the session became ready (READY or RESUMED).</returns>
    private bool HandleDispatch(GatewayPayload payload)
    {
        switch (payload.EventName)
        {
            case "READY":
                var ready = Deserialize(payload, DiscordApiJsonContext.Default.ReadyData);
                _sessionId = ready?.SessionId;
                _resumeUrl = Uri.TryCreate(ready?.ResumeGatewayUrl, UriKind.Absolute, out var resumeUrl) ? resumeUrl : null;
                CurrentUser = ready?.User;
                _state = GatewayState.Connected;
                Log.GatewayConnected(_logger, CurrentUser?.Username ?? "unknown bot");
                return true;

            case "RESUMED":
                _state = GatewayState.Connected;
                Log.GatewayResumed(_logger);
                return true;

            case "MESSAGE_CREATE":
                try
                {
                    if (Deserialize(payload, DiscordApiJsonContext.Default.GatewayMessage) is { } message)
                    {
                        _onMessage(message);
                    }
                }
#pragma warning disable CA1031 // A bad message or a handler bug must not drop the connection.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    Log.GatewayHandlerFailed(_logger, ex.GetType().Name, _token.Redact(ex.Message));
                }

                return false;

            default:
                return false;
        }
    }

    private async Task HeartbeatLoopAsync(IGatewayTransport transport, TimeSpan interval, HeartbeatState state, CancellationTokenSource session)
    {
        var token = session.Token;
        try
        {
            // First beat after interval * jitter, then every interval (gateway docs, "Sending Heartbeats").
            await _delay(interval * _random(), token).ConfigureAwait(false);
            while (true)
            {
                if (!state.Acknowledged)
                {
                    state.Zombie = true;
                    await session.CancelAsync().ConfigureAwait(false);
                    return;
                }

                state.Acknowledged = false;
                await transport.SendAsync(HeartbeatJson(), token).ConfigureAwait(false);
                await _delay(interval, token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or IOException or ObjectDisposedException)
        {
            // The receive loop notices the broken connection and decides what to do.
        }
    }

    private static SessionOutcome Closed(int? closeCode, bool wasReady)
    {
        var reason = closeCode is { } code ? string.Create(CultureInfo.InvariantCulture, $"close code {code}") : "connection closed";
        return GatewayCloseCodes.Classify(closeCode) switch
        {
            CloseAction.Fatal => new(SessionEnd.Fatal, GatewayCloseCodes.DescribeFatal(closeCode!.Value), wasReady),
            CloseAction.Reidentify => new(SessionEnd.Reidentify, reason, wasReady),
            _ => new(SessionEnd.Resume, reason, wasReady),
        };
    }

    private void ResetSession()
    {
        _sessionId = null;
        _resumeUrl = null;
        Interlocked.Exchange(ref _sequence, NoSequence);
    }

    private string HeartbeatJson()
    {
        var sequence = Interlocked.Read(ref _sequence);
        return sequence == NoSequence
            ? """{"op":1,"d":null}"""
            : string.Create(CultureInfo.InvariantCulture, $$"""{"op":1,"d":{{sequence}}}""");
    }

    private string IdentifyJson()
    {
        var os = OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux" : "other";
        var payload = new IdentifyPayload(new IdentifyData(_token.Value, Intents, new IdentifyProperties(os, "cs2-discord-relay", "cs2-discord-relay")));
        return JsonSerializer.Serialize(payload, DiscordApiJsonContext.Default.IdentifyPayload);
    }

    private string ResumeJson()
    {
        var sequence = Interlocked.Read(ref _sequence);
        var payload = new ResumePayload(new ResumeData(_token.Value, _sessionId!, sequence == NoSequence ? null : (int)sequence));
        return JsonSerializer.Serialize(payload, DiscordApiJsonContext.Default.ResumePayload);
    }

    private static Uri WithApiQuery(Uri url) => new UriBuilder(url) { Query = ApiQuery }.Uri;

    private static bool TryParse(string text, out GatewayPayload payload)
    {
        try
        {
            payload = JsonSerializer.Deserialize(text, DiscordApiJsonContext.Default.GatewayPayload)!;
            return payload is not null;
        }
        catch (JsonException)
        {
            payload = null!;
            return false;
        }
    }

    private static T? Deserialize<T>(GatewayPayload payload, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        if (payload.Data is not { ValueKind: not JsonValueKind.Null } data)
        {
            return default;
        }

        try
        {
            return data.Deserialize(typeInfo);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private enum SessionEnd
    {
        Resume,
        Reidentify,
        Fatal,
        Shutdown,
    }

    private readonly record struct SessionOutcome(SessionEnd Action, string Reason, bool WasReady, bool RandomWait = false);

    private sealed class HeartbeatState
    {
        private volatile bool _acknowledged = true;
        private volatile bool _zombie;

        public bool Acknowledged
        {
            get => _acknowledged;
            set => _acknowledged = value;
        }

        public bool Zombie
        {
            get => _zombie;
            set => _zombie = value;
        }
    }
}
