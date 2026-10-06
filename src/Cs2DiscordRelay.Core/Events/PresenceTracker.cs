namespace Cs2DiscordRelay.Core.Events;

/// <summary>
/// Hides the disconnect/reconnect pair CS2 produces for every player on a map change,
/// so Discord only sees real arrivals and departures.
/// </summary>
/// <remarks>
/// <para>
/// On <c>changelevel</c> the engine fires <c>player_disconnect</c> with a "loop" reason
/// (<c>NETWORK_DISCONNECT_LOOPSHUTDOWN</c>, <c>LOOPDEACTIVATE</c>...) and players connect again
/// once the new map is loaded. Such disconnects are held back; when the same player connects
/// again within <see cref="ReturnWindow"/>, neither event is announced.
/// </para>
/// <para>Not thread-safe: call it from the game thread only.</para>
/// </remarks>
public sealed class PresenceTracker
{
    /// <summary>How long a player has to come back after a map change before being considered gone.</summary>
    public static readonly TimeSpan ReturnWindow = TimeSpan.FromMinutes(3);

    // ENetworkDisconnectionReason values seen on map change or server shutdown, not real departures:
    // SHUTDOWN, RECONNECTION, LOOPSHUTDOWN, LOOPDEACTIVATE, HOST_ENDGAME, LOOP_LEVELLOAD_ACTIVATE, SERVER_SHUTDOWN.
    private static readonly HashSet<int> TransientReasons = [1, 53, 54, 55, 56, 57, 69];

    private readonly HashSet<string> _present = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _expectedBack = new(StringComparer.Ordinal);
    private bool _mapChanging;

    /// <summary>Call when the current map ends (<c>OnMapEnd</c>).</summary>
    public void MapEnding() => _mapChanging = true;

    /// <summary>Call when a new map starts (<c>OnMapStart</c>).</summary>
    public void MapStarted() => _mapChanging = false;

    /// <summary>Tells whether a connection is a real arrival.</summary>
    /// <param name="player">The player.</param>
    /// <param name="now">Current time.</param>
    /// <returns><see langword="false"/> for a player coming back after a map change.</returns>
    public bool ShouldAnnounceConnect(PlayerInfo player, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(player);
        var key = Key(player);

        if (_expectedBack.Remove(key, out var leftAt) && now - leftAt <= ReturnWindow)
        {
            _present.Add(key);
            return false;
        }

        // Already announced: the engine re-signs players on without a disconnect in between.
        return _present.Add(key);
    }

    /// <summary>Tells whether a disconnection is a real departure.</summary>
    /// <param name="player">The player.</param>
    /// <param name="reasonCode">Raw <c>player_disconnect.reason</c>.</param>
    /// <param name="now">Current time.</param>
    /// <returns><see langword="false"/> for the transient disconnect of a map change.</returns>
    public bool ShouldAnnounceDisconnect(PlayerInfo player, int reasonCode, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(player);
        var key = Key(player);
        _present.Remove(key);
        PurgeExpired(now);

        if (_mapChanging || TransientReasons.Contains(reasonCode))
        {
            _expectedBack[key] = now;
            return false;
        }

        return true;
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        if (_expectedBack.Count == 0)
        {
            return;
        }

        foreach (var (key, leftAt) in _expectedBack.ToArray())
        {
            if (now - leftAt > ReturnWindow)
            {
                _expectedBack.Remove(key);
            }
        }
    }

    private static string Key(PlayerInfo player) =>
        player.SteamId64 != 0 ? player.SteamId64.ToString(System.Globalization.CultureInfo.InvariantCulture) : "name:" + player.Name;
}
