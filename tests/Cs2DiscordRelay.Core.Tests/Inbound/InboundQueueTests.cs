using Cs2DiscordRelay.Core.Inbound;

namespace Cs2DiscordRelay.Core.Tests.Inbound;

public sealed class InboundQueueTests
{
    [Fact]
    public void Drains_fresh_lines_in_order_and_discards_stale_ones()
    {
        var queue = new InboundQueue();
        queue.Enqueue(new GameChatLine("a", "old"), TestData.Now);
        queue.Enqueue(new GameChatLine("b", "fresh"), TestData.Now.AddSeconds(25));
        queue.Enqueue(new GameChatLine("c", "newest"), TestData.Now.AddSeconds(29));

        var lines = new List<GameChatLine>();
        var stale = queue.Drain(TestData.Now.AddSeconds(31), InboundOptions.MaxAge, lines);

        Assert.Equal(1, stale);
        Assert.Equal(["fresh", "newest"], lines.Select(l => l.Message));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Keeps_only_the_most_recent_lines_when_full()
    {
        var queue = new InboundQueue();
        for (var i = 0; i < InboundQueue.Capacity + 10; i++)
        {
            queue.Enqueue(new GameChatLine("n", i.ToString(System.Globalization.CultureInfo.InvariantCulture)), TestData.Now);
        }

        var lines = new List<GameChatLine>();
        queue.Drain(TestData.Now, InboundOptions.MaxAge, lines);

        Assert.Equal(InboundQueue.Capacity, lines.Count);
        Assert.Equal("10", lines[0].Message);
    }
}
