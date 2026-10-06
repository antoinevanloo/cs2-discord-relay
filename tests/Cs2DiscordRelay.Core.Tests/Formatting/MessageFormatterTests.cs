using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Events;
using Cs2DiscordRelay.Core.Formatting;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Core.Tests.Formatting;

public sealed class MessageFormatterTests
{
    private static readonly RelayOptions EmbedOptions = new RelayOptions().Normalize([]);
    private static readonly RelayOptions TextOptions = (EmbedOptions with { Format = MessageFormat.Text }).Normalize([]);

    private static string Text(RelayEvent e, RelayOptions? options = null) =>
        new MessageFormatter(options ?? TextOptions).Format(e).Text!;

    private static DiscordEmbed Embed(RelayEvent e, RelayOptions? options = null) =>
        new MessageFormatter(options ?? EmbedOptions).Format(e).Embed!;

    [Fact]
    public void Text_global_chat_shows_name_and_message() =>
        Assert.Equal("💬 **s1mple**: gg wp", Text(TestData.Chat()));

    [Fact]
    public void Text_team_chat_has_a_distinct_prefix_and_team() =>
        Assert.Equal("💬 [TEAM] **s1mple** (Counter-Terrorists): rush B", Text(TestData.Chat("rush B", team: true)));

    [Fact]
    public void Text_team_chat_without_team_omits_it() =>
        Assert.Equal(
            "💬 [TEAM] **s1mple**: hi",
            Text(TestData.Chat("hi", team: true, TestData.Player(team: PlayerTeam.None))));

    [Fact]
    public void Text_escapes_player_name_and_message() =>
        Assert.Equal(
            @"💬 **\*\*admin\*\***: \@everyone free \[skins\]\(https\://x\.y\)",
            Text(TestData.Chat("@everyone free [skins](https://x.y)", player: TestData.Player("**admin**"))));

    [Fact]
    public void Text_connect_links_the_steam_profile_without_preview() =>
        Assert.Equal(
            $"🟢 **s1mple** joined the server ([{TestData.SteamId}](<https://steamcommunity.com/profiles/{TestData.SteamId}>))",
            Text(TestData.Connected()));

    [Fact]
    public void Text_connect_hides_steam_id_when_disabled() =>
        Assert.Equal(
            "🟢 **s1mple** joined the server",
            Text(TestData.Connected(), TextOptions with { ShowSteamId = false }));

    [Fact]
    public void Text_disconnect_includes_escaped_reason() =>
        Assert.Equal(
            @"🔴 **s1mple** left the server — kicked\_by\_admin",
            Text(TestData.Disconnected("kicked_by_admin"), TextOptions with { ShowSteamId = false }));

    [Fact]
    public void Text_map_change_escapes_underscores() =>
        Assert.Equal(@"🗺️ Map changed to **de\_dust2**", Text(TestData.Map()));

    [Fact]
    public void Text_is_truncated_to_the_content_limit()
    {
        var text = Text(TestData.Chat(new string('a', 5000)));

        Assert.Equal(DiscordLimits.ContentLength, text.Length);
    }

    [Fact]
    public void Embed_chat_uses_author_footer_color_and_timestamp()
    {
        var embed = Embed(TestData.Chat("nice *shot*"));

        Assert.Equal("s1mple", embed.Author!.Name);
        Assert.Equal($"https://steamcommunity.com/profiles/{TestData.SteamId}", embed.Author.Url);
        Assert.Equal(@"nice \*shot\*", embed.Description);
        Assert.Equal(0x5865F2, embed.Color);
        Assert.Equal("All chat", embed.Footer!.Text);
        Assert.Equal(TestData.Now, embed.Timestamp);
    }

    [Fact]
    public void Embed_author_name_is_sanitized_but_not_escaped()
    {
        // Discord renders author names as plain text: escaping would show the backslashes.
        var embed = Embed(TestData.Chat(player: TestData.Player("x_X‮_sniper")));

        Assert.Equal("x_X_sniper", embed.Author!.Name);
    }

    [Fact]
    public void Embed_team_chat_has_its_own_color_and_footer()
    {
        var embed = Embed(TestData.Chat(team: true, player: TestData.Player(team: PlayerTeam.Terrorist)));

        Assert.Equal(0xFEE75C, embed.Color);
        Assert.Equal("Team chat · Terrorists", embed.Footer!.Text);
    }

    [Fact]
    public void Embed_colors_come_from_options()
    {
        var options = (EmbedOptions with
        {
            Colors = new EventColors { PlayerConnect = "#010203", PlayerDisconnect = "#0A0B0C", MapChange = "#FFFFFF" },
        }).Normalize([]);

        Assert.Multiple(
            () => Assert.Equal(0x010203, Embed(TestData.Connected(), options).Color),
            () => Assert.Equal(0x0A0B0C, Embed(TestData.Disconnected(), options).Color),
            () => Assert.Equal(0xFFFFFF, Embed(TestData.Map(), options).Color));
    }

    [Fact]
    public void Embed_connect_shows_steam_id_in_footer()
    {
        var embed = Embed(TestData.Connected());

        Assert.Equal("🟢 **s1mple** joined the server", embed.Description);
        Assert.Equal($"SteamID64 {TestData.SteamId}", embed.Footer!.Text);
    }

    [Fact]
    public void Embed_disconnect_adds_reason_on_a_second_line()
    {
        var embed = Embed(TestData.Disconnected("Timed out"));

        Assert.Equal("🔴 **s1mple** left the server\nReason: Timed out", embed.Description);
    }

    [Fact]
    public void Embed_never_links_bots_or_unknown_steam_ids()
    {
        var embed = Embed(TestData.Chat(player: new PlayerInfo("Unauthed", 0, PlayerTeam.Spectator)));

        Assert.Null(embed.Author!.Url);
    }

    [Fact]
    public void Embed_map_change_has_a_title()
    {
        var embed = Embed(TestData.Map("cs_office"));

        Assert.Equal("🗺️ Map changed", embed.Title);
        Assert.Equal(@"**cs\_office**", embed.Description);
    }

    [Fact]
    public void Embed_description_is_truncated_to_the_embed_limit()
    {
        var embed = Embed(TestData.Chat(new string('*', 5000)));

        // Escaping doubles the length; the cut drops the escape that lost its character.
        Assert.InRange(embed.Description!.Length, DiscordLimits.EmbedDescriptionLength - 1, DiscordLimits.EmbedDescriptionLength);
        Assert.EndsWith(@"\*" + DiscordMarkdown.Ellipsis, embed.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_names_are_replaced() =>
        Assert.Equal("💬 **unnamed**: hi", Text(TestData.Chat("hi", player: TestData.Player("​"))));

    [Fact]
    public void Text_test_message_names_the_requester() =>
        Assert.Equal(
            @"✅ Discord relay is working. Test requested by **\_admin\_**.",
            Text(TestData.Test("_admin_")));

    [Fact]
    public void Embed_test_message_has_a_title_and_the_connect_color()
    {
        var embed = Embed(TestData.Test());

        Assert.Equal("✅ Discord relay is working", embed.Title);
        Assert.EndsWith("Test requested by **Console**.", embed.Description, StringComparison.Ordinal);
        Assert.Equal(0x57F287, embed.Color);
        Assert.Equal(TestData.Now, embed.Timestamp);
    }
}
