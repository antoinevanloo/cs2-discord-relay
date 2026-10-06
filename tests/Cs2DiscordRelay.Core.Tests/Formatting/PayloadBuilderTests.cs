using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Formatting;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Core.Tests.Formatting;

public sealed class PayloadBuilderTests
{
    private static readonly RelayOptions Options = new RelayOptions
    {
        WebhookUsername = "My CS2 server",
        WebhookAvatarUrl = "https://cdn.example.com/logo.png",
    }.Normalize([]);

    private static FormattedMessage Line(int length = 10) => FormattedMessage.FromText(new string('a', length));

    private static FormattedMessage Embed(int descriptionLength = 10) =>
        FormattedMessage.FromEmbed(new DiscordEmbed { Description = new string('a', descriptionLength) });

    [Fact]
    public void Empty_burst_produces_no_payload() =>
        Assert.Empty(PayloadBuilder.Build([], Options));

    [Fact]
    public void Text_lines_are_joined_in_order()
    {
        var payloads = PayloadBuilder.Build(
            [FormattedMessage.FromText("one"), FormattedMessage.FromText("two"), FormattedMessage.FromText("three")],
            Options);

        var payload = Assert.Single(payloads);
        Assert.Equal("one\ntwo\nthree", payload.Content);
        Assert.Equal(WebhookPayload.SuppressEmbedsFlag, payload.Flags);
    }

    [Fact]
    public void Text_is_split_before_exceeding_the_content_limit()
    {
        var payloads = PayloadBuilder.Build([Line(1500), Line(500), Line(1)], Options);

        Assert.Equal(2, payloads.Count);
        Assert.Equal(1500, payloads[0].Content!.Length);
        Assert.Equal(500 + 1 + 1, payloads[1].Content!.Length);
        Assert.All(payloads, p => Assert.True(p.Content!.Length <= DiscordLimits.ContentLength));
    }

    [Fact]
    public void Text_exactly_at_the_limit_stays_in_one_payload() =>
        Assert.Single(PayloadBuilder.Build([Line(1500), Line(499)], Options));

    [Fact]
    public void Embeds_are_split_by_ten()
    {
        var payloads = PayloadBuilder.Build(Enumerable.Range(0, 23).Select(_ => Embed()).ToArray(), Options);

        Assert.Equal([10, 10, 3], payloads.Select(p => p.Embeds!.Count));
    }

    [Fact]
    public void Embeds_are_split_before_exceeding_the_total_length()
    {
        var payloads = PayloadBuilder.Build([Embed(4000), Embed(1999), Embed(2)], Options);

        Assert.Equal([2, 1], payloads.Select(p => p.Embeds!.Count));
        Assert.All(payloads, p => Assert.True(p.Embeds!.Sum(e => e.TextLength) <= DiscordLimits.EmbedsTotalLength));
    }

    [Fact]
    public void Mixed_messages_keep_their_order()
    {
        var payloads = PayloadBuilder.Build([Line(), Embed(), Line(), Line()], Options);

        Assert.Equal(3, payloads.Count);
        Assert.NotNull(payloads[0].Content);
        Assert.NotNull(payloads[1].Embeds);
        Assert.NotNull(payloads[2].Content);
    }

    [Fact]
    public void Every_payload_carries_identity_and_blocks_mentions()
    {
        var payloads = PayloadBuilder.Build([Line(), Embed()], Options);

        Assert.All(payloads, p =>
        {
            Assert.Equal("My CS2 server", p.Username);
            Assert.Equal("https://cdn.example.com/logo.png", p.AvatarUrl);
            Assert.Empty(p.AllowedMentions.Parse);
        });
    }
}
