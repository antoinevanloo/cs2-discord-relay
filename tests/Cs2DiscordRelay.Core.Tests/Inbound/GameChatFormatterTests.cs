using Cs2DiscordRelay.Core.Inbound;

namespace Cs2DiscordRelay.Core.Tests.Inbound;

public sealed class GameChatFormatterTests
{
    private static readonly InboundOptions Defaults = new InboundOptions().Normalize([]);

    private static string Message(string content, Dictionary<ulong, string>? mentions = null) =>
        GameChatFormatter.Format(TestData.DiscordMessage(content, mentions: mentions), Defaults)!.Message;

    [Fact]
    public void Plain_messages_are_kept() =>
        Assert.Equal(new GameChatLine("Nova", "gg wp"), GameChatFormatter.Format(TestData.DiscordMessage("gg wp"), Defaults));

    [Theory]
    [InlineData("\u0002red \u0004green", "red green")]
    [InlineData("line one\nline two\r\nthree", "line one line two three")]
    [InlineData("\u0007\u0010\u000B", null)]
    public void Control_characters_and_newlines_never_reach_the_game(string content, string? expected) =>
        Assert.Equal(expected, GameChatFormatter.Format(TestData.DiscordMessage(content), Defaults)?.Message);

    [Fact]
    public void Color_codes_in_names_are_stripped() =>
        Assert.Equal("ADMIN", GameChatFormatter.Format(TestData.DiscordMessage(author: "\u0002ADMIN"), Defaults)!.Name);

    [Fact]
    public void Resolves_user_mentions_from_the_payload() =>
        Assert.Equal("@Nova hi @user", Message("<@7> hi <@!8>", new() { [7] = "Nova" }));

    [Theory]
    [InlineData("<@&123> meeting", "@role meeting")]
    [InlineData("see <#456>", "see #channel")]
    [InlineData("nice <:pog:789> <a:dance:790>", "nice :pog: :dance:")]
    [InlineData("use </rank:1>", "use /rank")]
    [InlineData("starts <t:1700000000:R>", "starts 2023-11-14 22:13 UTC")]
    [InlineData("starts <t:1700000000>", "starts 2023-11-14 22:13 UTC")]
    public void Converts_discord_tokens_to_text(string content, string expected) =>
        Assert.Equal(expected, Message(content));

    [Theory]
    [InlineData("**bold** __under__ ~~strike~~", "bold under strike")]
    [InlineData("`code` and ```cs\nvar x = 1;```", "code and var x = 1;")]
    [InlineData("the answer is ||42||", "the answer is [spoiler]")]
    [InlineData("[my site](https://example.com) <https://example.org>", "my site https://example.org")]
    [InlineData("# Title\n> quoted\n-# small", "Title quoted small")]
    [InlineData(@"5 \* 3 \_ok\_", "5 * 3 _ok_")]
    [InlineData("2*3*4 and snake_case", "2*3*4 and snake_case")]
    public void Strips_markdown(string content, string expected) =>
        Assert.Equal(expected, Message(content));

    [Fact]
    public void Describes_attachments_and_stickers()
    {
        var line = GameChatFormatter.Format(TestData.DiscordMessage("look", images: 2, files: 1, stickers: ["Wave"]), Defaults);

        Assert.Equal("look [2 images] [file] [sticker: Wave]", line!.Message);
    }

    [Fact]
    public void Attachment_only_messages_are_kept() =>
        Assert.Equal("[image]", GameChatFormatter.Format(TestData.DiscordMessage(string.Empty, images: 1), Defaults)!.Message);

    [Fact]
    public void Truncates_long_messages_and_names()
    {
        var line = GameChatFormatter.Format(TestData.DiscordMessage(new string('a', 500), author: new string('n', 100)), Defaults)!;

        Assert.Equal(Defaults.MaxMessageLength, line.Message.Length);
        Assert.Equal(InboundOptions.MaxNameLength, line.Name.Length);
    }

    [Fact]
    public void Render_fills_placeholders_once()
    {
        var rendered = GameChatFormatter.Render("[D] {name}: {MESSAGE}", new GameChatLine("{Message}", "{Name} {Red}"));

        Assert.Equal("[D] {Message}: {Name} {Red}", rendered);
    }
}
