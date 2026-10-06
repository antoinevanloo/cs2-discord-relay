namespace Cs2DiscordRelay.Core.Options;

/// <summary>How events are rendered in Discord.</summary>
public enum MessageFormat
{
    /// <summary>Rich embeds with a color per event type.</summary>
    Embed = 0,

    /// <summary>Compact one-line text messages.</summary>
    Text = 1,
}
