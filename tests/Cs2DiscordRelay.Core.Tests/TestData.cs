using Cs2DiscordRelay.Core.Events;

namespace Cs2DiscordRelay.Core.Tests;

internal static class TestData
{
    public const ulong SteamId = 76561197960287930UL;

    /// <summary>Has the shape of a bot token, but is obviously not one (and not flagged by secret scanners).</summary>
    public const string BotToken = "fake-token-for-tests.abcd.not-a-real-secret-0123456789";

    public static readonly DateTimeOffset Now = new(2026, 10, 6, 20, 30, 0, TimeSpan.Zero);

    public static PlayerInfo Player(string name = "s1mple", PlayerTeam team = PlayerTeam.CounterTerrorist, bool bot = false) =>
        new(name, bot ? 0 : SteamId, team, bot);

    public static ChatMessageEvent Chat(string message = "gg wp", bool team = false, PlayerInfo? player = null) =>
        new(Now, player ?? Player(), message, team);

    public static PlayerConnectedEvent Connected(PlayerInfo? player = null) => new(Now, player ?? Player());

    public static PlayerDisconnectedEvent Disconnected(string? reason = null, PlayerInfo? player = null) =>
        new(Now, player ?? Player(), reason);

    public static MapChangedEvent Map(string name = "de_dust2") => new(Now, name);

    public static RelayTestEvent Test(string requestedBy = "Console") => new(Now, requestedBy);

    public const ulong ChannelId = 555;
    public const ulong BotUserId = 42;

    public static Core.Inbound.DiscordMessage DiscordMessage(
        string content = "hello",
        string author = "Nova",
        ulong authorId = 7,
        int type = 0,
        ulong channelId = ChannelId,
        bool bot = false,
        bool webhook = false,
        ulong[]? roles = null,
        Dictionary<ulong, string>? mentions = null,
        int images = 0,
        int files = 0,
        string[]? stickers = null) =>
        new(channelId, type, content, authorId, author, bot, webhook, roles ?? [], mentions ?? [], images, files, stickers ?? []);
}
