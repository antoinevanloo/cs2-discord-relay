using Cs2DiscordRelay.Core.Formatting;

namespace Cs2DiscordRelay.Core.Tests.Formatting;

public sealed class DiscordMarkdownTests
{
    [Theory]
    [InlineData("**bold**", @"\*\*bold\*\*")]
    [InlineData("__underline__", @"\_\_underline\_\_")]
    [InlineData("~~strike~~", @"\~\~strike\~\~")]
    [InlineData("||spoiler||", @"\|\|spoiler\|\|")]
    [InlineData("`code`", @"\`code\`")]
    [InlineData("```block```", @"\`\`\`block\`\`\`")]
    [InlineData("> quote", @"\> quote")]
    [InlineData("# heading", @"\# heading")]
    [InlineData("-# subtext", @"\-\# subtext")]
    [InlineData(@"back\slash", @"back\\slash")]
    public void Escape_neutralizes_markdown(string input, string expected) =>
        Assert.Equal(expected, DiscordMarkdown.Escape(input));

    [Theory]
    [InlineData("@everyone", @"\@everyone")]
    [InlineData("@here", @"\@here")]
    [InlineData("<@123456789>", @"\<\@123456789\>")]
    [InlineData("<@&987>", @"\<\@&987\>")]
    [InlineData("<#42>", @"\<\#42\>")]
    [InlineData("<:pepe:123>", @"\<\:pepe\:123\>")]
    public void Escape_neutralizes_mentions_and_custom_emoji(string input, string expected) =>
        Assert.Equal(expected, DiscordMarkdown.Escape(input));

    [Fact]
    public void Escape_breaks_masked_links_and_autolinks()
    {
        var escaped = DiscordMarkdown.Escape("[free skins](https://evil.example)");

        Assert.Equal(@"\[free skins\]\(https\://evil\.example\)", escaped);
        Assert.DoesNotContain("https://", escaped, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("gg wp", "gg wp")]
    [InlineData("héhé ça marche 👍", "héhé ça marche 👍")]
    [InlineData("1v5 clutch", "1v5 clutch")]
    public void Escape_leaves_plain_text_untouched(string input, string expected) =>
        Assert.Equal(expected, DiscordMarkdown.Escape(input));

    [Fact]
    public void Escape_handles_null_and_empty() =>
        Assert.Multiple(
            () => Assert.Equal(string.Empty, DiscordMarkdown.Escape(null)),
            () => Assert.Equal(string.Empty, DiscordMarkdown.Escape(string.Empty)));

    [Theory]
    [InlineData("line1\nline2", "line1 line2")]
    [InlineData("  padded \t text  ", "padded text")]
    [InlineData("bell\u0007char", "bellchar")]
    [InlineData("rtl‮override", "rtloverride")]
    [InlineData("zero​width", "zerowidth")]
    [InlineData("﻿bom", "bom")]
    public void Sanitize_strips_control_and_invisible_characters(string input, string expected) =>
        Assert.Equal(expected, DiscordMarkdown.Sanitize(input));

    [Fact]
    public void Sanitize_keeps_zero_width_joiner_for_emoji_sequences()
    {
        const string family = "👨‍👩‍👧";

        Assert.Equal(family, DiscordMarkdown.Sanitize(family));
    }

    [Fact]
    public void Truncate_returns_text_unchanged_when_it_fits() =>
        Assert.Equal("short", DiscordMarkdown.Truncate("short", 10));

    [Fact]
    public void Truncate_appends_ellipsis_within_the_limit()
    {
        var result = DiscordMarkdown.Truncate(new string('a', 50), 10);

        Assert.Equal(10, result.Length);
        Assert.EndsWith(DiscordMarkdown.Ellipsis, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_never_splits_a_surrogate_pair()
    {
        // "aaaa😀" : the emoji occupies indexes 4-5; a cut at 5 would split it.
        var result = DiscordMarkdown.Truncate("aaaa😀bbbb", 6);

        Assert.Equal("aaaa" + DiscordMarkdown.Ellipsis, result);
    }

    [Fact]
    public void Truncate_never_leaves_a_dangling_escape()
    {
        var escaped = DiscordMarkdown.Escape("abc*def");   // abc\*def
        var result = DiscordMarkdown.Truncate(escaped, 5); // would cut right after the backslash

        Assert.Equal("abc" + DiscordMarkdown.Ellipsis, result);
    }

    [Fact]
    public void Truncate_keeps_complete_escape_pairs()
    {
        var result = DiscordMarkdown.Truncate(@"ab\\cdef", 5); // cut after an escaped backslash

        Assert.Equal(@"ab\\" + DiscordMarkdown.Ellipsis, result);
    }
}
