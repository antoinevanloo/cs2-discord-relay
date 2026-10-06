using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Core.Tests.Options;

public sealed class RelayOptionsTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var warnings = new List<string>();

        var options = new RelayOptions().Normalize(warnings);

        Assert.Empty(warnings);
        Assert.Equal(MessageFormat.Embed, options.Format);
        Assert.Equal(["!", "/"], options.CommandPrefixes);
    }

    [Fact]
    public void Invalid_colors_fall_back_to_defaults()
    {
        var warnings = new List<string>();
        var options = new RelayOptions
        {
            Colors = new EventColors { Chat = "blue", TeamChat = "#12345", MapChange = "#00ff00" },
        }.Normalize(warnings);

        Assert.Equal(EventColors.DefaultChat, options.Colors.Chat);
        Assert.Equal(EventColors.DefaultTeamChat, options.Colors.TeamChat);
        Assert.Equal("#00ff00", options.Colors.MapChange);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        var warnings = new List<string>();
        var options = new RelayOptions
        {
            BatchWindow = TimeSpan.FromMinutes(5),
            MaxQueueSize = 1,
        }.Normalize(warnings);

        Assert.Equal(RelayOptions.MaxBatchWindow, options.BatchWindow);
        Assert.Equal(RelayOptions.MinQueueSize, options.MaxQueueSize);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Negative_batch_window_disables_batching()
    {
        var options = new RelayOptions { BatchWindow = TimeSpan.FromSeconds(-1) }.Normalize([]);

        Assert.Equal(TimeSpan.Zero, options.BatchWindow);
    }

    [Fact]
    public void Undefined_format_falls_back_to_embed()
    {
        var warnings = new List<string>();

        var options = new RelayOptions { Format = (MessageFormat)42 }.Normalize(warnings);

        Assert.Equal(MessageFormat.Embed, options.Format);
        Assert.Single(warnings);
    }

    [Fact]
    public void Command_prefixes_are_trimmed_deduplicated_and_blank_ones_removed()
    {
        var options = new RelayOptions { CommandPrefixes = ["!", " ! ", "", "  ", ".", "/"] }.Normalize([]);

        Assert.Equal(["!", ".", "/"], options.CommandPrefixes);
    }

    [Theory]
    [InlineData("", null, 0)]
    [InlineData("  My Server  ", "My Server", 0)]
    public void Username_is_trimmed_or_cleared(string input, string? expected, int warningCount)
    {
        var warnings = new List<string>();

        var options = new RelayOptions { WebhookUsername = input }.Normalize(warnings);

        Assert.Equal(expected, options.WebhookUsername);
        Assert.Equal(warningCount, warnings.Count);
    }

    [Fact]
    public void Username_longer_than_discord_limit_is_truncated()
    {
        var warnings = new List<string>();

        var options = new RelayOptions { WebhookUsername = new string('x', 100) }.Normalize(warnings);

        Assert.Equal(80, options.WebhookUsername!.Length);
        Assert.Single(warnings);
    }

    [Theory]
    [InlineData("https://cdn.example.com/logo.png", true)]
    [InlineData("ftp://example.com/logo.png", false)]
    [InlineData("not a url", false)]
    public void Avatar_url_must_be_http(string input, bool kept)
    {
        var options = new RelayOptions { WebhookAvatarUrl = input }.Normalize([]);

        Assert.Equal(kept ? input : null, options.WebhookAvatarUrl);
    }

    [Theory]
    [InlineData("#5865F2", true, 0x5865F2)]
    [InlineData("5865f2", true, 0x5865F2)]
    [InlineData(" #FFFFFF ", true, 0xFFFFFF)]
    [InlineData("#FFF", false, 0)]
    [InlineData("#GGGGGG", false, 0)]
    [InlineData(null, false, 0)]
    public void HexColor_parses_rrggbb(string? input, bool ok, int expected)
    {
        Assert.Equal(ok, HexColor.TryParse(input, out var color));
        Assert.Equal(expected, color);
    }
}
