using System.Globalization;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Events;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Core.Formatting;

/// <summary>Renders <see cref="RelayEvent"/>s as Discord text lines or embeds.</summary>
/// <remarks>Every piece of player-controlled text is sanitized and escaped here.</remarks>
public sealed class MessageFormatter
{
    private const string TeamChatPrefix = "[TEAM]";

    private readonly RelayOptions _options;
    private readonly int _chatColor;
    private readonly int _teamChatColor;
    private readonly int _connectColor;
    private readonly int _disconnectColor;
    private readonly int _mapColor;

    /// <summary>Creates a formatter.</summary>
    /// <param name="options">Normalized options (see <see cref="RelayOptions.Normalize"/>).</param>
    public MessageFormatter(RelayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _chatColor = ParseColor(options.Colors.Chat, EventColors.DefaultChat);
        _teamChatColor = ParseColor(options.Colors.TeamChat, EventColors.DefaultTeamChat);
        _connectColor = ParseColor(options.Colors.PlayerConnect, EventColors.DefaultPlayerConnect);
        _disconnectColor = ParseColor(options.Colors.PlayerDisconnect, EventColors.DefaultPlayerDisconnect);
        _mapColor = ParseColor(options.Colors.MapChange, EventColors.DefaultMapChange);
    }

    /// <summary>Renders an event using the configured <see cref="MessageFormat"/>.</summary>
    /// <param name="relayEvent">The event.</param>
    /// <returns>The rendered message.</returns>
    public FormattedMessage Format(RelayEvent relayEvent)
    {
        ArgumentNullException.ThrowIfNull(relayEvent);

        return _options.Format == MessageFormat.Text
            ? FormattedMessage.FromText(DiscordMarkdown.Truncate(FormatText(relayEvent), DiscordLimits.ContentLength))
            : FormattedMessage.FromEmbed(FormatEmbed(relayEvent));
    }

    /// <summary>Steam community profile URL for a SteamID64.</summary>
    /// <param name="steamId64">The SteamID64.</param>
    /// <returns>The profile URL.</returns>
    public static string SteamProfileUrl(ulong steamId64) =>
        string.Create(CultureInfo.InvariantCulture, $"https://steamcommunity.com/profiles/{steamId64}");

    /// <summary>Human readable team name, or <see langword="null"/> when the player has no team.</summary>
    /// <param name="team">The team.</param>
    /// <returns>The team name.</returns>
    public static string? TeamName(PlayerTeam team) => team switch
    {
        PlayerTeam.Terrorist => "Terrorists",
        PlayerTeam.CounterTerrorist => "Counter-Terrorists",
        PlayerTeam.Spectator => "Spectators",
        _ => null,
    };

    private string FormatText(RelayEvent relayEvent) => relayEvent switch
    {
        ChatMessageEvent { TeamOnly: true } chat =>
            $"💬 {TeamChatPrefix} **{Name(chat.Player)}**{TeamSuffix(chat.Player.Team)}: {DiscordMarkdown.Escape(chat.Message)}",
        ChatMessageEvent chat =>
            $"💬 **{Name(chat.Player)}**: {DiscordMarkdown.Escape(chat.Message)}",
        PlayerConnectedEvent connected =>
            $"🟢 **{Name(connected.Player)}** joined the server{SteamIdSuffix(connected.Player)}",
        PlayerDisconnectedEvent disconnected =>
            $"🔴 **{Name(disconnected.Player)}** left the server{SteamIdSuffix(disconnected.Player)}{ReasonSuffix(disconnected.Reason)}",
        MapChangedEvent map =>
            $"🗺️ Map changed to **{DiscordMarkdown.Escape(map.MapName)}**",
        _ => throw new ArgumentOutOfRangeException(nameof(relayEvent), relayEvent.GetType().Name, "Unsupported event type."),
    };

    private DiscordEmbed FormatEmbed(RelayEvent relayEvent) => relayEvent switch
    {
        ChatMessageEvent chat => new DiscordEmbed
        {
            Author = Author(chat.Player),
            Description = Description(DiscordMarkdown.Escape(chat.Message)),
            Color = chat.TeamOnly ? _teamChatColor : _chatColor,
            Footer = new EmbedFooter(chat.TeamOnly
                ? TeamName(chat.Player.Team) is { } team ? $"Team chat · {team}" : "Team chat"
                : "All chat"),
            Timestamp = chat.Timestamp,
        },
        PlayerConnectedEvent connected => new DiscordEmbed
        {
            Description = Description($"🟢 **{Name(connected.Player)}** joined the server"),
            Color = _connectColor,
            Footer = SteamIdFooter(connected.Player),
            Timestamp = connected.Timestamp,
        },
        PlayerDisconnectedEvent disconnected => new DiscordEmbed
        {
            Description = Description(
                $"🔴 **{Name(disconnected.Player)}** left the server"
                + (string.IsNullOrWhiteSpace(disconnected.Reason) ? string.Empty : $"\nReason: {DiscordMarkdown.Escape(disconnected.Reason)}")),
            Color = _disconnectColor,
            Footer = SteamIdFooter(disconnected.Player),
            Timestamp = disconnected.Timestamp,
        },
        MapChangedEvent map => new DiscordEmbed
        {
            Title = "🗺️ Map changed",
            Description = Description($"**{DiscordMarkdown.Escape(map.MapName)}**"),
            Color = _mapColor,
            Timestamp = map.Timestamp,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(relayEvent), relayEvent.GetType().Name, "Unsupported event type."),
    };

    private static string Name(PlayerInfo player)
    {
        var name = DiscordMarkdown.Escape(player.Name);
        return name.Length == 0 ? "unnamed" : DiscordMarkdown.Truncate(name, DiscordLimits.EmbedAuthorNameLength);
    }

    private static string Description(string text) => DiscordMarkdown.Truncate(text, DiscordLimits.EmbedDescriptionLength);

    private EmbedAuthor Author(PlayerInfo player)
    {
        // Embed author names are rendered as plain text: sanitize, but do not escape.
        var name = DiscordMarkdown.Sanitize(player.Name);
        var author = new EmbedAuthor(DiscordMarkdown.Truncate(name.Length == 0 ? "unnamed" : name, DiscordLimits.EmbedAuthorNameLength));
        return ShowsSteamId(player) ? author with { Url = SteamProfileUrl(player.SteamId64) } : author;
    }

    private EmbedFooter? SteamIdFooter(PlayerInfo player) =>
        ShowsSteamId(player)
            ? new EmbedFooter(string.Create(CultureInfo.InvariantCulture, $"SteamID64 {player.SteamId64}"))
            : null;

    private string SteamIdSuffix(PlayerInfo player) =>
        ShowsSteamId(player)
            ? string.Create(CultureInfo.InvariantCulture, $" ([{player.SteamId64}](<{SteamProfileUrl(player.SteamId64)}>))")
            : string.Empty;

    private static string TeamSuffix(PlayerTeam team) =>
        TeamName(team) is { } name ? $" ({name})" : string.Empty;

    private static string ReasonSuffix(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? string.Empty : $" — {DiscordMarkdown.Escape(reason)}";

    private bool ShowsSteamId(PlayerInfo player) => _options.ShowSteamId && player.SteamId64 != 0 && !player.IsBot;

    private static int ParseColor(string value, string fallback) =>
        HexColor.TryParse(value, out var color) ? color : HexColor.TryParse(fallback, out var f) ? f : 0;
}
