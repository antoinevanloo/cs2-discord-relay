using Cs2DiscordRelay.Core.Events;
using Cs2DiscordRelay.Core.Formatting;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Core.Tests.Formatting;

public sealed class EventFilterTests
{
    private static readonly RelayOptions Defaults = new RelayOptions().Normalize([]);

    [Theory]
    [InlineData("!rtv")]
    [InlineData("/ws")]
    [InlineData("   !knife")]
    public void Commands_are_ignored_by_default(string message) =>
        Assert.False(EventFilter.ShouldRelay(TestData.Chat(message), Defaults));

    [Theory]
    [InlineData("hello !")]
    [InlineData("1/2 hp")]
    [InlineData("gg")]
    public void Regular_messages_are_relayed(string message) =>
        Assert.True(EventFilter.ShouldRelay(TestData.Chat(message), Defaults));

    [Fact]
    public void Commands_are_relayed_when_the_filter_is_disabled()
    {
        var options = Defaults with { IgnoreCommands = false };

        Assert.True(EventFilter.ShouldRelay(TestData.Chat("!rtv"), options));
    }

    [Fact]
    public void Custom_prefixes_replace_the_defaults()
    {
        var options = (Defaults with { CommandPrefixes = [".", "css_"] }).Normalize([]);

        Assert.Multiple(
            () => Assert.False(EventFilter.ShouldRelay(TestData.Chat(".stats"), options)),
            () => Assert.False(EventFilter.ShouldRelay(TestData.Chat("css_help"), options)),
            () => Assert.True(EventFilter.ShouldRelay(TestData.Chat("!rtv"), options)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("​‮")]
    public void Empty_or_invisible_messages_are_ignored(string message) =>
        Assert.False(EventFilter.ShouldRelay(TestData.Chat(message), Defaults));

    [Fact]
    public void Bots_are_ignored_by_default_and_can_be_included()
    {
        var bot = TestData.Player("BOT Kevin", bot: true);

        Assert.Multiple(
            () => Assert.False(EventFilter.ShouldRelay(TestData.Connected(bot), Defaults)),
            () => Assert.False(EventFilter.ShouldRelay(TestData.Disconnected(player: bot), Defaults)),
            () => Assert.False(EventFilter.ShouldRelay(TestData.Chat(player: bot), Defaults)),
            () => Assert.True(EventFilter.ShouldRelay(TestData.Connected(bot), Defaults with { IgnoreBots = false })));
    }

    public static TheoryData<RelayEvent, Action<EventToggles>> ToggledEvents => new()
    {
        { TestData.Chat(team: false), t => t.Chat = false },
        { TestData.Chat(team: true), t => t.TeamChat = false },
        { TestData.Connected(), t => t.PlayerConnect = false },
        { TestData.Disconnected(), t => t.PlayerDisconnect = false },
        { TestData.Map(), t => t.MapChange = false },
    };

    [Theory]
    [MemberData(nameof(ToggledEvents))]
    public void Each_event_type_can_be_disabled(RelayEvent relayEvent, Action<EventToggles> disable)
    {
        var toggles = new EventToggles();
        disable(toggles);

        Assert.True(EventFilter.ShouldRelay(relayEvent, Defaults));
        Assert.False(EventFilter.ShouldRelay(relayEvent, Defaults with { Events = toggles }));
    }

    [Fact]
    public void Disabling_global_chat_keeps_team_chat()
    {
        var options = Defaults with { Events = new EventToggles { Chat = false } };

        Assert.True(EventFilter.ShouldRelay(TestData.Chat(team: true), options));
    }
}
