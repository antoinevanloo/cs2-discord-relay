using CounterStrikeSharp.API.Core;
using Cs2DiscordRelay.Core.Events;

namespace Cs2DiscordRelay;

/// <summary>
/// Copies what the relay needs out of a player controller. Must run on the game thread:
/// controllers are native objects that are not valid once the callback returns.
/// </summary>
internal static class PlayerSnapshot
{
    public static PlayerInfo From(CCSPlayerController player)
    {
        var isBot = player.IsBot;
        return new PlayerInfo(
            player.PlayerName ?? string.Empty,
            isBot ? 0 : player.SteamID,
            (PlayerTeam)(int)player.Team,
            isBot);
    }
}
