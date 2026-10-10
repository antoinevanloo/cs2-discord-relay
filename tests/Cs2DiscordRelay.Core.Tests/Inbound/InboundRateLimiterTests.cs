using Cs2DiscordRelay.Core.Inbound;

namespace Cs2DiscordRelay.Core.Tests.Inbound;

public sealed class InboundRateLimiterTests
{
    private static readonly DateTimeOffset T0 = TestData.Now;

    [Fact]
    public void Enforces_a_cooldown_per_author()
    {
        var limiter = new InboundRateLimiter(new InboundOptions { UserCooldown = TimeSpan.FromSeconds(2) });

        Assert.True(limiter.TryAcquire(1, T0));
        Assert.False(limiter.TryAcquire(1, T0.AddSeconds(1.9)));
        Assert.True(limiter.TryAcquire(2, T0.AddSeconds(1.9)));
        Assert.True(limiter.TryAcquire(1, T0.AddSeconds(2)));
        Assert.Equal(1, limiter.TakeDropped());
        Assert.Equal(0, limiter.TakeDropped());
    }

    [Fact]
    public void Enforces_a_global_quota_per_window()
    {
        var limiter = new InboundRateLimiter(new InboundOptions { MaxMessagesPer10Seconds = 3, UserCooldown = TimeSpan.Zero });

        for (ulong user = 1; user <= 3; user++)
        {
            Assert.True(limiter.TryAcquire(user, T0));
        }

        Assert.False(limiter.TryAcquire(4, T0.AddSeconds(9.9)));
        Assert.True(limiter.TryAcquire(4, T0.AddSeconds(10)));
    }
}
