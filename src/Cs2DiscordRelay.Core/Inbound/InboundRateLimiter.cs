namespace Cs2DiscordRelay.Core.Inbound;

/// <summary>
/// Keeps Discord from flooding the game chat: a global quota per <see cref="InboundOptions.RateWindow"/>
/// plus a cooldown per author. Messages over the limit are dropped and counted.
/// </summary>
internal sealed class InboundRateLimiter
{
    private readonly int _maxPerWindow;
    private readonly TimeSpan _userCooldown;
    private readonly Queue<DateTimeOffset> _recent = new();
    private readonly Dictionary<ulong, DateTimeOffset> _lastByUser = [];
    private readonly Lock _lock = new();
    private long _dropped;

    public InboundRateLimiter(InboundOptions options)
    {
        _maxPerWindow = options.MaxMessagesPer10Seconds;
        _userCooldown = options.UserCooldown;
    }

    /// <summary>Messages dropped since the last call to <see cref="TakeDropped"/>.</summary>
    public long TakeDropped() => Interlocked.Exchange(ref _dropped, 0);

    public bool TryAcquire(ulong userId, DateTimeOffset now)
    {
        lock (_lock)
        {
            while (_recent.Count > 0 && now - _recent.Peek() >= InboundOptions.RateWindow)
            {
                _recent.Dequeue();
            }

            if (_lastByUser.Count > 1000)
            {
                foreach (var stale in _lastByUser.Where(e => now - e.Value >= _userCooldown).Select(e => e.Key).ToList())
                {
                    _lastByUser.Remove(stale);
                }
            }

            var coolingDown = _lastByUser.TryGetValue(userId, out var last) && now - last < _userCooldown;
            if (coolingDown || _recent.Count >= _maxPerWindow)
            {
                Interlocked.Increment(ref _dropped);
                return false;
            }

            _recent.Enqueue(now);
            _lastByUser[userId] = now;
            return true;
        }
    }
}
