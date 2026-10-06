using Cs2DiscordRelay.Core.Events;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Core.Formatting;

/// <summary>Decides whether an event should reach Discord, according to the options.</summary>
public static class EventFilter
{
    /// <summary>Applies event toggles, the bot filter and the chat command filter.</summary>
    /// <param name="relayEvent">The event.</param>
    /// <param name="options">Normalized options.</param>
    /// <returns><see langword="true"/> when the event must be relayed.</returns>
    public static bool ShouldRelay(RelayEvent relayEvent, RelayOptions options)
    {
        ArgumentNullException.ThrowIfNull(relayEvent);
        ArgumentNullException.ThrowIfNull(options);

        return relayEvent switch
        {
            ChatMessageEvent chat => (chat.TeamOnly ? options.Events.TeamChat : options.Events.Chat)
                && IsAllowedPlayer(chat.Player, options)
                && IsRelayableMessage(chat.Message, options),
            PlayerConnectedEvent connected => options.Events.PlayerConnect && IsAllowedPlayer(connected.Player, options),
            PlayerDisconnectedEvent disconnected => options.Events.PlayerDisconnect && IsAllowedPlayer(disconnected.Player, options),
            MapChangedEvent map => options.Events.MapChange && !string.IsNullOrWhiteSpace(map.MapName),
            _ => false,
        };
    }

    /// <summary>Tells whether <paramref name="message"/> starts with one of the configured command prefixes.</summary>
    /// <param name="message">Raw chat message.</param>
    /// <param name="prefixes">Command prefixes.</param>
    /// <returns><see langword="true"/> for chat commands such as <c>!rtv</c> or <c>/ws</c>.</returns>
    public static bool IsCommand(string message, IReadOnlyList<string> prefixes)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(prefixes);

        var trimmed = message.AsSpan().TrimStart();
        foreach (var prefix in prefixes)
        {
            if (prefix.Length > 0 && trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowedPlayer(PlayerInfo player, RelayOptions options) =>
        !(options.IgnoreBots && player.IsBot);

    private static bool IsRelayableMessage(string message, RelayOptions options) =>
        DiscordMarkdown.Sanitize(message).Length > 0
        && !(options.IgnoreCommands && IsCommand(message, options.CommandPrefixes));
}
