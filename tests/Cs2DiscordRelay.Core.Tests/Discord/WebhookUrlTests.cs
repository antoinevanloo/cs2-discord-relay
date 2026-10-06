using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests.Discord;

public sealed class WebhookUrlTests
{
    private const string Id = "123456789012345678";
    private const string Token = DispatcherHarness.Token;

    [Theory]
    [InlineData($"https://discord.com/api/webhooks/{Id}/{Token}")]
    [InlineData($"https://discordapp.com/api/webhooks/{Id}/{Token}")]
    [InlineData($"https://ptb.discord.com/api/webhooks/{Id}/{Token}")]
    [InlineData($"https://canary.discord.com/api/webhooks/{Id}/{Token}")]
    [InlineData($"https://discord.com/api/v10/webhooks/{Id}/{Token}")]
    [InlineData($"https://discord.com/api/webhooks/{Id}/{Token}/")]
    [InlineData($"  https://discord.com/api/webhooks/{Id}/{Token}  ")]
    public void Accepts_urls_copied_from_discord(string value)
    {
        Assert.True(WebhookUrl.TryParse(value, out var url, out var error));
        Assert.NotNull(url);
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null, "empty")]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("discord.com/api/webhooks/1/2", "absolute")]
    [InlineData($"http://discord.com/api/webhooks/{Id}/{Token}", "https")]
    [InlineData($"https://discord.evil.com/api/webhooks/{Id}/{Token}", "discord.com")]
    [InlineData($"https://discord.com:8443/api/webhooks/{Id}/{Token}", "discord.com")]
    [InlineData($"https://discord.com/api/webhooks/{Id}", "look like")]
    [InlineData($"https://discord.com/api/webhooks/not-a-number/{Token}", "look like")]
    [InlineData($"https://discord.com/api/webhooks/{Id}/short", "look like")]
    [InlineData($"https://discord.com/channels/{Id}/{Id}", "look like")]
    public void Rejects_invalid_urls_with_a_clear_reason(string? value, string expectedReason)
    {
        Assert.False(WebhookUrl.TryParse(value, out var url, out var error));
        Assert.Null(url);
        Assert.Contains(expectedReason, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData($"http://discord.com/api/webhooks/{Id}/{Token}")]
    [InlineData($"https://discord.evil.com/api/webhooks/{Id}/{Token}")]
    [InlineData($"https://discord.com/api/webhooks/{Id}/{Token}/extra")]
    public void Error_messages_never_contain_the_token(string value)
    {
        Assert.False(WebhookUrl.TryParse(value, out _, out var error));
        Assert.DoesNotContain(Token, error, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_is_redacted()
    {
        Assert.True(WebhookUrl.TryParse(DispatcherHarness.RawUrl, out var url, out _));

        Assert.Equal(WebhookUrl.Redacted, url.ToString());
        Assert.Equal(WebhookUrl.Redacted, $"{url}");
    }

    [Fact]
    public void Redact_removes_url_and_token_from_arbitrary_text()
    {
        Assert.True(WebhookUrl.TryParse(DispatcherHarness.RawUrl, out var url, out _));

        var redacted = url.Redact($"POST {DispatcherHarness.RawUrl} failed, token={Token}");

        Assert.DoesNotContain(Token, redacted, StringComparison.Ordinal);
        Assert.Contains(WebhookUrl.Redacted, redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Keeps_thread_id_and_drops_other_query_parameters()
    {
        Assert.True(WebhookUrl.TryParse($"{DispatcherHarness.RawUrl}?wait=true&thread_id=987654321098765432&x=1", out var url, out _));

        Assert.Equal("?thread_id=987654321098765432", url.Uri.Query);
    }
}
