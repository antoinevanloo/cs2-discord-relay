using Cs2DiscordRelay.Core.Events;

namespace Cs2DiscordRelay.Core.Tests;

internal static class TestData
{
    public const ulong SteamId = 76561197960287930UL;

    public static readonly DateTimeOffset Now = new(2026, 10, 6, 20, 30, 0, TimeSpan.Zero);

    public static PlayerInfo Player(string name = "s1mple", PlayerTeam team = PlayerTeam.CounterTerrorist, bool bot = false) =>
        new(name, bot ? 0 : SteamId, team, bot);

    public static ChatMessageEvent Chat(string message = "gg wp", bool team = false, PlayerInfo? player = null) =>
        new(Now, player ?? Player(), message, team);

    public static PlayerConnectedEvent Connected(PlayerInfo? player = null) => new(Now, player ?? Player());

    public static PlayerDisconnectedEvent Disconnected(string? reason = null, PlayerInfo? player = null) =>
        new(Now, player ?? Player(), reason);

    public static MapChangedEvent Map(string name = "de_dust2") => new(Now, name);
}
