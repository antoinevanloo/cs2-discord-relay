using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Cs2DiscordRelay.Core.Tests.Fakes;

/// <summary>Delays that only complete when the test says so, so timing is asserted exactly.</summary>
internal sealed class ManualDelays
{
    private readonly Channel<PendingDelay> _pending = Channel.CreateUnbounded<PendingDelay>();

    public ConcurrentQueue<TimeSpan> Requested { get; } = new();

    public Task Delay(TimeSpan duration, CancellationToken cancellationToken)
    {
        Requested.Enqueue(duration);
        var pending = new PendingDelay(duration, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        cancellationToken.Register(() => pending.Completion.TrySetCanceled(cancellationToken));
        _pending.Writer.TryWrite(pending);
        return pending.Completion.Task;
    }

    /// <summary>Next delay requested by the code under test that is still pending, matching <paramref name="predicate"/>.</summary>
    public async Task<PendingDelay> NextAsync(Func<TimeSpan, bool>? predicate = null)
    {
        while (true)
        {
            var next = await _pending.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (!next.Completion.Task.IsCompleted && (predicate is null || predicate(next.Duration)))
            {
                return next;
            }
        }
    }

    /// <summary>Waits for a pending delay of exactly <paramref name="duration"/> and completes it.</summary>
    public async Task ElapseAsync(TimeSpan duration) =>
        (await NextAsync(d => d == duration)).Completion.TrySetResult();
}

internal sealed record PendingDelay(TimeSpan Duration, TaskCompletionSource Completion)
{
    public void Elapse() => Completion.TrySetResult();
}
