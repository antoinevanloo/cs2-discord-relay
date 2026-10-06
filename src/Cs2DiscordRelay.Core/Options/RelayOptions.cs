namespace Cs2DiscordRelay.Core.Options;

/// <summary>Validated, game-agnostic relay settings.</summary>
public sealed record RelayOptions
{
    /// <summary>Lower bound for <see cref="MaxQueueSize"/>.</summary>
    public const int MinQueueSize = 10;

    /// <summary>Upper bound for <see cref="MaxQueueSize"/>.</summary>
    public const int MaxQueueSizeLimit = 10_000;

    /// <summary>Upper bound for <see cref="BatchWindow"/>.</summary>
    public static readonly TimeSpan MaxBatchWindow = TimeSpan.FromSeconds(10);

    /// <summary>Embed or plain text rendering.</summary>
    public MessageFormat Format { get; init; } = MessageFormat.Embed;

    /// <summary>Which events are relayed.</summary>
    public EventToggles Events { get; init; } = new();

    /// <summary>Embed colors per event type.</summary>
    public EventColors Colors { get; init; } = new();

    /// <summary>Skip chat messages that look like chat commands.</summary>
    public bool IgnoreCommands { get; init; } = true;

    /// <summary>Prefixes identifying chat commands, e.g. <c>!</c> and <c>/</c>.</summary>
    public IReadOnlyList<string> CommandPrefixes { get; init; } = ["!", "/"];

    /// <summary>Skip events caused by bots.</summary>
    public bool IgnoreBots { get; init; } = true;

    /// <summary>Show the player's SteamID64 and a link to their Steam profile.</summary>
    public bool ShowSteamId { get; init; } = true;

    /// <summary>Overrides the webhook's display name. <see langword="null"/> keeps the one set in Discord.</summary>
    public string? WebhookUsername { get; init; }

    /// <summary>Overrides the webhook's avatar. <see langword="null"/> keeps the one set in Discord.</summary>
    public string? WebhookAvatarUrl { get; init; }

    /// <summary>How long to wait for more events before sending, so bursts share one request. Zero disables batching.</summary>
    public TimeSpan BatchWindow { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Maximum number of pending messages; the oldest are dropped beyond that.</summary>
    public int MaxQueueSize { get; init; } = 500;

    /// <summary>
    /// Returns a copy where every invalid value is replaced by a safe default.
    /// Each correction is reported through <paramref name="warnings"/>.
    /// </summary>
    /// <param name="warnings">Receives one human readable message per corrected value.</param>
    /// <returns>The normalized options.</returns>
    public RelayOptions Normalize(ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        var colors = new EventColors
        {
            Chat = NormalizeColor(Colors?.Chat, EventColors.DefaultChat, nameof(EventColors.Chat), warnings),
            TeamChat = NormalizeColor(Colors?.TeamChat, EventColors.DefaultTeamChat, nameof(EventColors.TeamChat), warnings),
            PlayerConnect = NormalizeColor(Colors?.PlayerConnect, EventColors.DefaultPlayerConnect, nameof(EventColors.PlayerConnect), warnings),
            PlayerDisconnect = NormalizeColor(Colors?.PlayerDisconnect, EventColors.DefaultPlayerDisconnect, nameof(EventColors.PlayerDisconnect), warnings),
            MapChange = NormalizeColor(Colors?.MapChange, EventColors.DefaultMapChange, nameof(EventColors.MapChange), warnings),
        };

        var format = Format;
        if (!Enum.IsDefined(format))
        {
            warnings.Add($"MessageFormat '{format}' is not supported, falling back to {MessageFormat.Embed}.");
            format = MessageFormat.Embed;
        }

        var batchWindow = BatchWindow;
        if (batchWindow < TimeSpan.Zero || batchWindow > MaxBatchWindow)
        {
            batchWindow = batchWindow < TimeSpan.Zero ? TimeSpan.Zero : MaxBatchWindow;
            warnings.Add($"BatchWindowMs must be between 0 and {MaxBatchWindow.TotalMilliseconds}, using {batchWindow.TotalMilliseconds}.");
        }

        var queueSize = Math.Clamp(MaxQueueSize, MinQueueSize, MaxQueueSizeLimit);
        if (queueSize != MaxQueueSize)
        {
            warnings.Add($"MaxQueueSize must be between {MinQueueSize} and {MaxQueueSizeLimit}, using {queueSize}.");
        }

        var prefixes = (CommandPrefixes ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var username = string.IsNullOrWhiteSpace(WebhookUsername) ? null : WebhookUsername.Trim();
        if (username is { Length: > 80 })
        {
            warnings.Add("WebhookUsername is longer than 80 characters (Discord limit), it was truncated.");
            username = username[..80];
        }

        var avatar = string.IsNullOrWhiteSpace(WebhookAvatarUrl) ? null : WebhookAvatarUrl.Trim();
        if (avatar is not null
            && !(Uri.TryCreate(avatar, UriKind.Absolute, out var avatarUri)
                 && (avatarUri.Scheme == Uri.UriSchemeHttps || avatarUri.Scheme == Uri.UriSchemeHttp)))
        {
            warnings.Add("WebhookAvatarUrl is not an absolute http(s) URL, it was ignored.");
            avatar = null;
        }

        return this with
        {
            Format = format,
            Events = Events ?? new EventToggles(),
            Colors = colors,
            CommandPrefixes = prefixes,
            WebhookUsername = username,
            WebhookAvatarUrl = avatar,
            BatchWindow = batchWindow,
            MaxQueueSize = queueSize,
        };
    }

    private static string NormalizeColor(string? value, string fallback, string name, ICollection<string> warnings)
    {
        if (HexColor.TryParse(value, out _))
        {
            return value!.Trim();
        }

        warnings.Add($"Colors.{name} '{value}' is not a valid #RRGGBB color, using {fallback}.");
        return fallback;
    }
}
