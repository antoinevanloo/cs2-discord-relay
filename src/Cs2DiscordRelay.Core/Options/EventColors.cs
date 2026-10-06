namespace Cs2DiscordRelay.Core.Options;

/// <summary>Embed accent colors, as <c>#RRGGBB</c> hex strings.</summary>
public sealed class EventColors
{
    /// <summary>Default color for global chat (Discord blurple).</summary>
    public const string DefaultChat = "#5865F2";

    /// <summary>Default color for team chat (yellow).</summary>
    public const string DefaultTeamChat = "#FEE75C";

    /// <summary>Default color for connections (green).</summary>
    public const string DefaultPlayerConnect = "#57F287";

    /// <summary>Default color for disconnections (red).</summary>
    public const string DefaultPlayerDisconnect = "#ED4245";

    /// <summary>Default color for map changes (fuchsia).</summary>
    public const string DefaultMapChange = "#EB459E";

    /// <summary>Global chat.</summary>
    public string Chat { get; set; } = DefaultChat;

    /// <summary>Team chat.</summary>
    public string TeamChat { get; set; } = DefaultTeamChat;

    /// <summary>Player connections.</summary>
    public string PlayerConnect { get; set; } = DefaultPlayerConnect;

    /// <summary>Player disconnections.</summary>
    public string PlayerDisconnect { get; set; } = DefaultPlayerDisconnect;

    /// <summary>Map changes.</summary>
    public string MapChange { get; set; } = DefaultMapChange;
}
