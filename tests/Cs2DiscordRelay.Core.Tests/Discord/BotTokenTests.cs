using Cs2DiscordRelay.Core.Discord;

namespace Cs2DiscordRelay.Core.Tests.Discord;

public sealed class BotTokenTests
{
    [Theory]
    [InlineData(TestData.BotToken)]
    [InlineData("  " + TestData.BotToken + "  ")]
    [InlineData("Bot " + TestData.BotToken)]
    public void Accepts_tokens_copied_from_the_portal(string value)
    {
        Assert.True(BotToken.TryParse(value, out var token, out var error), error);
        Assert.Equal(TestData.BotToken, token.Value);
        Assert.Equal("Bot " + TestData.BotToken, token.AuthorizationHeader);
    }

    [Theory]
    [InlineData(null, "BotToken is empty.")]
    [InlineData("   ", "BotToken is empty.")]
    [InlineData("not-a-token", "BotToken does not look like")]
    [InlineData("abcdefghijkl.abcd.has spaces inside the last part", "BotToken does not look like")]
    public void Rejects_invalid_tokens_without_echoing_them(string? value, string expected)
    {
        Assert.False(BotToken.TryParse(value, out _, out var error));
        Assert.StartsWith(expected, error, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(value))
        {
            Assert.DoesNotContain(value, error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Never_shows_the_token()
    {
        Assert.True(BotToken.TryParse(TestData.BotToken, out var token, out _));

        Assert.Equal("***", token.ToString());
        Assert.Equal("auth failed for *** (401)", token.Redact($"auth failed for {TestData.BotToken} (401)"));
    }
}
