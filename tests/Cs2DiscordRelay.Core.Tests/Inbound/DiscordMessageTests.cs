using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Inbound;

namespace Cs2DiscordRelay.Core.Tests.Inbound;

public sealed class DiscordMessageTests
{
    private static GatewayMessage Parse(string json) =>
        System.Text.Json.JsonSerializer.Deserialize(json, DiscordApiJsonContext.Default.GatewayMessage)!;

    [Fact]
    public void Maps_a_message_create_payload()
    {
        var message = DiscordMessage.From(Parse("""
            {"id":"9","channel_id":"555","type":19,"content":"hi","webhook_id":null,
             "author":{"id":"7","username":"nova","global_name":"Nova"},
             "member":{"nick":"Nova CT","roles":["11","bad"]},
             "mentions":[{"id":"8","username":"ghost","member":{"nick":"Ghost T"}},{"id":"9","username":"plain"}],
             "attachments":[{"content_type":"image/png"},{"content_type":"application/zip"},{}],
             "sticker_items":[{"name":"Wave"}]}
            """))!;

        Assert.Equal(555UL, message.ChannelId);
        Assert.Equal(19, message.Type);
        Assert.Equal(7UL, message.AuthorId);
        Assert.Equal("Nova CT", message.AuthorName);
        Assert.False(message.IsWebhook);
        Assert.Equal([11UL], message.RoleIds);
        Assert.Equal("Ghost T", message.MentionNames[8]);
        Assert.Equal("plain", message.MentionNames[9]);
        Assert.Equal(1, message.ImageCount);
        Assert.Equal(2, message.FileCount);
        Assert.Equal(["Wave"], message.StickerNames);
    }

    [Theory]
    [InlineData("""{"author":{"id":"1","username":"u","global_name":"Global"}}""", "Global")]
    [InlineData("""{"author":{"id":"1","username":"u","global_name":null}}""", "u")]
    [InlineData("""{"author":{"id":"1","username":"u"},"member":{"nick":" "}}""", "u")]
    public void Display_name_prefers_nick_then_global_name_then_username(string extra, string expected)
    {
        var json = """{"id":"9","channel_id":"555","type":0,""" + extra[1..];

        Assert.Equal(expected, DiscordMessage.From(Parse(json))!.AuthorName);
    }

    [Fact]
    public void Flags_webhooks_and_bots()
    {
        var message = DiscordMessage.From(Parse("""{"id":"9","channel_id":"555","type":0,"webhook_id":"77","author":{"id":"77","username":"Relay","bot":true}}"""))!;

        Assert.True(message.IsWebhook);
        Assert.True(message.IsBot);
    }

    [Fact]
    public void Rejects_malformed_ids() =>
        Assert.Null(DiscordMessage.From(Parse("""{"id":"9","channel_id":"general","type":0,"author":{"id":"7"}}""")));
}
