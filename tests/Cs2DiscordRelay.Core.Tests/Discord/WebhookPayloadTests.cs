using System.Text.Json;
using Cs2DiscordRelay.Core.Discord;

namespace Cs2DiscordRelay.Core.Tests.Discord;

public sealed class WebhookPayloadTests
{
    [Fact]
    public void Serialize_always_includes_empty_allowed_mentions()
    {
        var json = WebhookJson.Serialize(new WebhookPayload { Content = "@everyone" });

        using var doc = JsonDocument.Parse(json);
        var parse = doc.RootElement.GetProperty("allowed_mentions").GetProperty("parse");
        Assert.Equal(JsonValueKind.Array, parse.ValueKind);
        Assert.Equal(0, parse.GetArrayLength());
    }

    [Fact]
    public void Serialize_uses_discord_snake_case_and_omits_nulls()
    {
        var payload = new WebhookPayload
        {
            Username = "CS2",
            AvatarUrl = "https://example.com/a.png",
            Embeds =
            [
                new DiscordEmbed
                {
                    Description = "hello",
                    Color = 0x5865F2,
                    Timestamp = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero),
                    Author = new EmbedAuthor("Player") { Url = "https://steamcommunity.com/profiles/1" },
                    Footer = new EmbedFooter("All chat"),
                },
            ],
        };

        var json = WebhookJson.Serialize(payload);

        Assert.Equal(
            """{"username":"CS2","avatar_url":"https://example.com/a.png","embeds":[{"description":"hello","color":5793266,"timestamp":"2026-10-06T12:00:00+00:00","author":{"name":"Player","url":"https://steamcommunity.com/profiles/1"},"footer":{"text":"All chat"}}],"allowed_mentions":{"parse":[]}}""",
            json);
    }

    [Fact]
    public void TextLength_counts_every_text_field()
    {
        var embed = new DiscordEmbed
        {
            Title = "12345",
            Description = "123",
            Author = new EmbedAuthor("12"),
            Footer = new EmbedFooter("1"),
        };

        Assert.Equal(11, embed.TextLength);
    }

    [Theory]
    [InlineData("""{"message":"You are being rate limited.","retry_after":1.5,"global":false}""", 1.5)]
    [InlineData("""{"message":"Unknown Webhook","code":10015}""", null)]
    public void TryParseError_reads_discord_error_bodies(string body, double? retryAfter) =>
        Assert.Equal(retryAfter, WebhookJson.TryParseError(body)?.RetryAfter);

    [Fact]
    public void TryParseError_returns_null_on_non_json_bodies() =>
        Assert.Null(WebhookJson.TryParseError("<html>bad gateway</html>"));
}
