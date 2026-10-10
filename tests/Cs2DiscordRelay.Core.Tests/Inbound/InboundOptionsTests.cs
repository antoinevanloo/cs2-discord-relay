using Cs2DiscordRelay.Core.Inbound;

namespace Cs2DiscordRelay.Core.Tests.Inbound;

public sealed class InboundOptionsTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        var warnings = new List<string>();

        var options = new InboundOptions().Normalize(warnings);

        Assert.Empty(warnings);
        Assert.Equal(InboundOptions.DefaultGameChatFormat, options.GameChatFormat);
    }

    [Fact]
    public void Corrects_invalid_values_with_one_warning_each()
    {
        var warnings = new List<string>();

        var options = new InboundOptions
        {
            GameChatFormat = "{Name} only",
            MaxMessageLength = 5,
            MaxMessagesPer10Seconds = 0,
            UserCooldown = TimeSpan.FromSeconds(-1),
            AllowedRoleIds = [1, 1, 2],
        }.Normalize(warnings);

        Assert.Equal(4, warnings.Count);
        Assert.Equal(InboundOptions.DefaultGameChatFormat, options.GameChatFormat);
        Assert.Equal(InboundOptions.MinMessageLengthLimit, options.MaxMessageLength);
        Assert.Equal(1, options.MaxMessagesPer10Seconds);
        Assert.Equal(TimeSpan.Zero, options.UserCooldown);
        Assert.Equal([1UL, 2UL], options.AllowedRoleIds);
    }

    [Theory]
    [InlineData("175928847299117063", true)]
    [InlineData(" 1 ", true)]
    [InlineData("0", false)]
    [InlineData("-5", false)]
    [InlineData("#general", false)]
    [InlineData(null, false)]
    public void Parses_snowflakes(string? value, bool valid) => Assert.Equal(valid, Snowflake.TryParse(value, out _));
}
