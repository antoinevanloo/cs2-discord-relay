using Cs2DiscordRelay.Core.Inbound;

namespace Cs2DiscordRelay;

/// <summary><c>"DiscordToGame"</c> section: shows messages from a Discord channel in the game chat, through a bot.</summary>
public sealed class DiscordToGameConfig
{
    /// <summary>Off by default: it needs a Discord bot (see docs/discord-bot-setup.md).</summary>
    public bool Enabled { get; set; }

    /// <summary>Bot token (secret), from Developer Portal &gt; Bot &gt; Reset Token.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Channel to read. Empty reads the channel of <c>WebhookUrl</c>.</summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>When not empty, only members with one of these roles reach the game chat.</summary>
    public List<string> AllowedRoleIds { get; set; } = [];

    /// <summary>In-game line: CounterStrikeSharp color tags, <c>{Name}</c> and <c>{Message}</c>.</summary>
    public string GameChatFormat { get; set; } = InboundOptions.DefaultGameChatFormat;

    /// <summary>Longer messages are truncated (20 to 400).</summary>
    public int MaxMessageLength { get; set; } = 200;

    /// <summary>Messages shown in game per 10 seconds, all authors together (1 to 50).</summary>
    public int MaxMessagesPer10Seconds { get; set; } = 5;

    /// <summary>Minimum seconds between two messages of the same author (0 to 60).</summary>
    public int UserCooldownSeconds { get; set; } = 2;

    /// <summary>Maps the JSON settings to normalized core options.</summary>
    /// <param name="warnings">Receives one message per corrected value.</param>
    /// <returns>The options.</returns>
    public InboundOptions ToInboundOptions(ICollection<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        ulong? channelId = null;
        if (!string.IsNullOrWhiteSpace(ChannelId))
        {
            if (Snowflake.TryParse(ChannelId, out var id))
            {
                channelId = id;
            }
            else
            {
                warnings.Add($"DiscordToGame.ChannelId '{ChannelId}' is not a Discord id (Developer Mode, then right-click the channel > Copy Channel ID). Reading the webhook's channel instead.");
            }
        }

        var roles = new List<ulong>();
        foreach (var role in AllowedRoleIds ?? [])
        {
            if (Snowflake.TryParse(role, out var id))
            {
                roles.Add(id);
            }
            else
            {
                warnings.Add($"DiscordToGame.AllowedRoleIds entry '{role}' is not a Discord id, it was ignored.");
            }
        }

        return new InboundOptions
        {
            ChannelId = channelId,
            AllowedRoleIds = roles,
            GameChatFormat = GameChatFormat,
            MaxMessageLength = MaxMessageLength,
            MaxMessagesPer10Seconds = MaxMessagesPer10Seconds,
            UserCooldown = TimeSpan.FromSeconds(UserCooldownSeconds),
        }.Normalize(warnings);
    }
}
