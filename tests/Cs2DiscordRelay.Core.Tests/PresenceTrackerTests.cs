using Cs2DiscordRelay.Core.Events;

namespace Cs2DiscordRelay.Core.Tests;

public sealed class PresenceTrackerTests
{
    private const int TimedOut = 29;
    private const int LoopDeactivate = 55;
    private const int LoopShutdown = 54;

    private static readonly PlayerInfo Player = TestData.Player();
    private static readonly DateTimeOffset T0 = TestData.Now;

    [Fact]
    public void Real_arrival_and_departure_are_announced()
    {
        var tracker = new PresenceTracker();

        Assert.True(tracker.ShouldAnnounceConnect(Player, T0));
        Assert.True(tracker.ShouldAnnounceDisconnect(Player, TimedOut, T0.AddMinutes(10)));
    }

    [Theory]
    [InlineData(LoopShutdown)]
    [InlineData(LoopDeactivate)]
    [InlineData(57)]
    public void Map_change_disconnect_and_reconnect_are_hidden(int reason)
    {
        var tracker = new PresenceTracker();
        tracker.ShouldAnnounceConnect(Player, T0);

        Assert.False(tracker.ShouldAnnounceDisconnect(Player, reason, T0.AddMinutes(20)));
        Assert.False(tracker.ShouldAnnounceConnect(Player, T0.AddMinutes(20).AddSeconds(30)));
    }

    [Fact]
    public void Any_disconnect_during_map_change_is_hidden()
    {
        var tracker = new PresenceTracker();
        tracker.ShouldAnnounceConnect(Player, T0);

        tracker.MapEnding();
        Assert.False(tracker.ShouldAnnounceDisconnect(Player, 2, T0.AddMinutes(1)));
        tracker.MapStarted();

        Assert.False(tracker.ShouldAnnounceConnect(Player, T0.AddMinutes(1.5)));
    }

    [Fact]
    public void Disconnects_after_the_map_started_are_announced_again()
    {
        var tracker = new PresenceTracker();
        tracker.MapEnding();
        tracker.MapStarted();
        tracker.ShouldAnnounceConnect(Player, T0);

        Assert.True(tracker.ShouldAnnounceDisconnect(Player, 2, T0.AddMinutes(5)));
    }

    [Fact]
    public void Coming_back_after_the_window_is_a_new_arrival()
    {
        var tracker = new PresenceTracker();
        tracker.ShouldAnnounceConnect(Player, T0);
        tracker.ShouldAnnounceDisconnect(Player, LoopDeactivate, T0);

        Assert.True(tracker.ShouldAnnounceConnect(Player, T0 + PresenceTracker.ReturnWindow + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Players_present_before_a_hot_reload_stay_silent_across_a_map_change()
    {
        var tracker = new PresenceTracker(); // never saw the player connect

        Assert.False(tracker.ShouldAnnounceDisconnect(Player, LoopDeactivate, T0));
        Assert.False(tracker.ShouldAnnounceConnect(Player, T0.AddSeconds(20)));
    }

    [Fact]
    public void Duplicate_connect_without_disconnect_is_announced_once()
    {
        var tracker = new PresenceTracker();

        Assert.True(tracker.ShouldAnnounceConnect(Player, T0));
        Assert.False(tracker.ShouldAnnounceConnect(Player, T0.AddSeconds(5)));
    }

    [Fact]
    public void Players_are_tracked_independently()
    {
        var tracker = new PresenceTracker();
        var other = new PlayerInfo("ZywOo", 76561198000000001UL, PlayerTeam.Terrorist);
        tracker.ShouldAnnounceConnect(Player, T0);
        tracker.ShouldAnnounceDisconnect(Player, LoopDeactivate, T0);

        Assert.True(tracker.ShouldAnnounceConnect(other, T0.AddSeconds(10)));
    }
}
