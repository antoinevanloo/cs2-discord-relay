using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay;

/// <summary>
/// Shape of <c>addons/counterstrikesharp/configs/plugins/Cs2DiscordRelay/Cs2DiscordRelay.json</c>.
/// CounterStrikeSharp generates the file with these defaults on first load.
/// </summary>
public sealed class PluginConfig : BasePluginConfig
{
    /// <summary>Discord webhook URL (secret). The relay stays idle while it is empty.</summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary><c>"Embed"</c> or <c>"Text"</c>.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<MessageFormat>))]
    public MessageFormat MessageFormat { get; set; } = MessageFormat.Embed;

    /// <summary>Which events are relayed.</summary>
    public EventToggles Events { get; set; } = new();

    /// <summary>Skip chat messages starting with one of <see cref="CommandPrefixes"/>.</summary>
    public bool IgnoreCommands { get; set; } = true;

    /// <summary>Chat command prefixes.</summary>
    public List<string> CommandPrefixes { get; set; } = ["!", "/"];

    /// <summary>Skip events caused by bots.</summary>
    public bool IgnoreBots { get; set; } = true;

    /// <summary>Show SteamID64 and a link to the Steam profile.</summary>
    public bool ShowSteamId { get; set; } = true;

    /// <summary>Embed colors, <c>#RRGGBB</c>.</summary>
    public EventColors Colors { get; set; } = new();

    /// <summary>Overrides the webhook name. Empty keeps the name set in Discord.</summary>
    public string WebhookUsername { get; set; } = string.Empty;

    /// <summary>Overrides the webhook avatar. Empty keeps the avatar set in Discord.</summary>
    public string WebhookAvatarUrl { get; set; } = string.Empty;

    /// <summary>Milliseconds to wait for more events before sending a burst. 0 disables batching.</summary>
    public int BatchWindowMs { get; set; } = 1000;

    /// <summary>Maximum pending messages before the oldest are dropped.</summary>
    public int MaxQueueSize { get; set; } = 500;

    /// <summary>Maps the JSON settings to normalized core options.</summary>
    /// <param name="warnings">Receives one message per corrected value.</param>
    /// <returns>The options.</returns>
    public RelayOptions ToRelayOptions(ICollection<string> warnings) => new RelayOptions
    {
        Format = MessageFormat,
        Events = Events,
        IgnoreCommands = IgnoreCommands,
        CommandPrefixes = CommandPrefixes,
        IgnoreBots = IgnoreBots,
        ShowSteamId = ShowSteamId,
        Colors = Colors,
        WebhookUsername = WebhookUsername,
        WebhookAvatarUrl = WebhookAvatarUrl,
        BatchWindow = TimeSpan.FromMilliseconds(BatchWindowMs),
        MaxQueueSize = MaxQueueSize,
    }.Normalize(warnings);
}
