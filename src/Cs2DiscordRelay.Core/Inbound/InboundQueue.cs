using System.Threading.Channels;

namespace Cs2DiscordRelay.Core.Inbound;

/// <summary>
/// Hands lines from the gateway thread to the game thread. Bounded (oldest dropped) and time-stamped, so
/// nothing piles up while the server hibernates and stale lines are never shown.
/// </summary>
internal sealed class InboundQueue
{
    public const int Capacity = 50;

    private readonly Channel<(DateTimeOffset ReceivedAt, GameChatLine Line)> _channel =
        Channel.CreateBounded<(DateTimeOffset, GameChatLine)>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

    public int Count => _channel.Reader.Count;

    public void Enqueue(GameChatLine line, DateTimeOffset receivedAt) => _channel.Writer.TryWrite((receivedAt, line));

    /// <summary>Moves fresh lines to <paramref name="into"/>, oldest first.</summary>
    /// <returns>Number of lines discarded because they waited longer than <paramref name="maxAge"/>.</returns>
    public int Drain(DateTimeOffset now, TimeSpan maxAge, ICollection<GameChatLine> into)
    {
        var stale = 0;
        while (_channel.Reader.TryRead(out var entry))
        {
            if (now - entry.ReceivedAt > maxAge)
            {
                stale++;
            }
            else
            {
                into.Add(entry.Line);
            }
        }

        return stale;
    }
}
