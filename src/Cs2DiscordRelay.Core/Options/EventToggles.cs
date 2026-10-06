namespace Cs2DiscordRelay.Core.Options;

/// <summary>Enables or disables each relayed event type.</summary>
public sealed class EventToggles
{
    /// <summary>Global (all) chat messages.</summary>
    public bool Chat { get; set; } = true;

    /// <summary>Team chat messages.</summary>
    public bool TeamChat { get; set; } = true;

    /// <summary>Player connections.</summary>
    public bool PlayerConnect { get; set; } = true;

    /// <summary>Player disconnections.</summary>
    public bool PlayerDisconnect { get; set; } = true;

    /// <summary>Map changes.</summary>
    public bool MapChange { get; set; } = true;
}
