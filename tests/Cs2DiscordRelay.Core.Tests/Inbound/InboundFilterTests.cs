using Cs2DiscordRelay.Core.Inbound;

namespace Cs2DiscordRelay.Core.Tests.Inbound;

public sealed class InboundFilterTests
{
    private static readonly InboundOptions Defaults = new InboundOptions().Normalize([]);

    private static bool Relayed(DiscordMessage message, InboundOptions? options = null) =>
        InboundFilter.ShouldRelay(message, TestData.ChannelId, TestData.BotUserId, options ?? Defaults);

    [Fact]
    public void Relays_messages_and_replies_from_people_in_the_channel()
    {
        Assert.True(Relayed(TestData.DiscordMessage()));
        Assert.True(Relayed(TestData.DiscordMessage(type: 19)));
    }

    [Fact]
    public void Ignores_other_channels() => Assert.False(Relayed(TestData.DiscordMessage(channelId: 999)));

    [Theory]
    [InlineData(6)] // pinned message notice
    [InlineData(7)] // member joined
    [InlineData(20)] // slash command
    public void Ignores_system_and_command_messages(int type) => Assert.False(Relayed(TestData.DiscordMessage(type: type)));

    [Fact]
    public void Ignores_bots_webhooks_and_itself()
    {
        Assert.False(Relayed(TestData.DiscordMessage(bot: true)));
        Assert.False(Relayed(TestData.DiscordMessage(webhook: true))); // includes the relay's own posts: no echo loop
        Assert.False(Relayed(TestData.DiscordMessage(authorId: TestData.BotUserId)));
    }

    [Fact]
    public void Allowed_roles_restrict_who_reaches_the_game()
    {
        var options = Defaults with { AllowedRoleIds = [100, 200] };

        Assert.True(Relayed(TestData.DiscordMessage(roles: [5, 200]), options));
        Assert.False(Relayed(TestData.DiscordMessage(roles: [5]), options));
        Assert.False(Relayed(TestData.DiscordMessage(), options));
    }
}
