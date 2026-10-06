using Cs2DiscordRelay.Core.Events;

namespace Cs2DiscordRelay.Core.Tests;

public sealed class DisconnectReasonsTests
{
    [Theory]
    [InlineData(2, "Disconnected")]
    [InlineData(29, "Timed out")]
    [InlineData(39, "Kicked")]
    [InlineData(41, "Kicked and banned")]
    public void Known_codes_have_a_label(int code, string label) =>
        Assert.Equal(label, DisconnectReasons.Describe(code));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(9999)]
    public void Unknown_codes_have_no_label(int code) =>
        Assert.Null(DisconnectReasons.Describe(code));
}
