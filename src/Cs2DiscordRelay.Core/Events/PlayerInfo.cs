namespace Cs2DiscordRelay.Core.Events;

/// <summary>Plain data copied from the game's player controller.</summary>
/// <param name="Name">Display name, as chosen by the player (untrusted input).</param>
/// <param name="SteamId64">64-bit SteamID, or <c>0</c> when unknown (bots, not yet authenticated).</param>
/// <param name="Team">Team the player belongs to.</param>
/// <param name="IsBot"><see langword="true"/> for bots.</param>
public sealed record PlayerInfo(string Name, ulong SteamId64, PlayerTeam Team, bool IsBot = false);

/// <summary>CS2 teams, mirroring CounterStrikeSharp's <c>CsTeam</c> values.</summary>
public enum PlayerTeam
{
    /// <summary>Not assigned to a team yet.</summary>
    None = 0,

    /// <summary>Spectators.</summary>
    Spectator = 1,

    /// <summary>Terrorists.</summary>
    Terrorist = 2,

    /// <summary>Counter-Terrorists.</summary>
    CounterTerrorist = 3,
}
