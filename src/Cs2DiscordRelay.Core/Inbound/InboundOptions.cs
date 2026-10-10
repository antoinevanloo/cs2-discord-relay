namespace Cs2DiscordRelay.Core.Inbound;

/// <summary>Validated settings of the Discord → game relay.</summary>
public sealed record InboundOptions
{
    /// <summary>Default in-game line. Color tags are CounterStrikeSharp's <c>ChatColors</c> names.</summary>
    public const string DefaultGameChatFormat = "{LightBlue}[Discord]{Default} {Green}{Name}{Default}: {Message}";

    /// <summary>Longest author name shown in game.</summary>
    public const int MaxNameLength = 32;

    /// <summary>Bounds for <see cref="MaxMessageLength"/>.</summary>
    public const int MinMessageLengthLimit = 20;

    /// <summary>Bounds for <see cref="MaxMessageLength"/>.</summary>
    public const int MaxMessageLengthLimit = 400;

    /// <summary>Messages waiting longer than this (server hibernating, map loading) are not shown anymore.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(30);

    /// <summary>Window of <see cref="MaxMessagesPer10Seconds"/>.</summary>
    public static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(10);

    /// <summary>Discord channel (or thread) to read. <see langword="null"/> uses the webhook's channel.</summary>
    public ulong? ChannelId { get; init; }

    /// <summary>When not empty, only members with one of these roles reach the game chat.</summary>
    public IReadOnlyList<ulong> AllowedRoleIds { get; init; } = [];

    /// <summary>In-game line with color tags, <c>{Name}</c> and <c>{Message}</c>.</summary>
    public string GameChatFormat { get; init; } = DefaultGameChatFormat;

    /// <summary>Longer messages are truncated.</summary>
    public int MaxMessageLength { get; init; } = 200;

    /// <summary>Messages shown in game per 10 seconds, all authors together.</summary>
    public int MaxMessagesPer10Seconds { get; init; } = 5;

    /// <summary>Minimum delay between two messages of the same author.</summary>
    public TimeSpan UserCooldown { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Returns a copy where every invalid value is replaced by a safe default, reporting each correction.</summary>
    /// <param name="warnings">Receives one human readable message per corrected value.</param>
    /// <returns>The normalized options.</returns>
    public InboundOptions Normalize(ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        var format = GameChatFormat;
        if (string.IsNullOrWhiteSpace(format) || !format.Contains("{Message}", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("DiscordToGame.GameChatFormat must contain {Message}, using the default format.");
            format = DefaultGameChatFormat;
        }

        var length = Math.Clamp(MaxMessageLength, MinMessageLengthLimit, MaxMessageLengthLimit);
        if (length != MaxMessageLength)
        {
            warnings.Add($"DiscordToGame.MaxMessageLength must be between {MinMessageLengthLimit} and {MaxMessageLengthLimit}, using {length}.");
        }

        var rate = Math.Clamp(MaxMessagesPer10Seconds, 1, 50);
        if (rate != MaxMessagesPer10Seconds)
        {
            warnings.Add($"DiscordToGame.MaxMessagesPer10Seconds must be between 1 and 50, using {rate}.");
        }

        var cooldown = UserCooldown;
        if (cooldown < TimeSpan.Zero || cooldown > TimeSpan.FromMinutes(1))
        {
            cooldown = cooldown < TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromMinutes(1);
            warnings.Add($"DiscordToGame.UserCooldownSeconds must be between 0 and 60, using {cooldown.TotalSeconds}.");
        }

        return this with
        {
            AllowedRoleIds = [.. (AllowedRoleIds ?? []).Distinct()],
            GameChatFormat = format,
            MaxMessageLength = length,
            MaxMessagesPer10Seconds = rate,
            UserCooldown = cooldown,
        };
    }
}
