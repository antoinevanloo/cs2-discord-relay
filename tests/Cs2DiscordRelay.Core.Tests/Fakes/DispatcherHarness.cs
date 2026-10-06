using System.Collections.Concurrent;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Formatting;
using Cs2DiscordRelay.Core.Options;
using Microsoft.Extensions.Time.Testing;

namespace Cs2DiscordRelay.Core.Tests.Fakes;

/// <summary>
/// Wires a dispatcher to fakes. Delays complete immediately but advance the fake clock,
/// so rate limit waits are asserted exactly and tests never sleep.
/// </summary>
internal sealed class DispatcherHarness : IAsyncDisposable
{
    public const string Token = "abcdefghijklmnopqrstuvwxyz_ABCDEFGHIJKLMNOPQRSTUVWXYZ-0123456789abcd";
    public const string RawUrl = "https://discord.com/api/webhooks/123456789012345678/" + Token;

    private readonly HttpClient _http;

    public DispatcherHarness(RelayOptions? options = null, params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
        : this(RawUrl, options, responses)
    {
    }

    public DispatcherHarness(string rawUrl, RelayOptions? options, params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        Assert.True(WebhookUrl.TryParse(rawUrl, out var url, out _));
        Url = url;
        Handler = new FakeHttpHandler(responses);
        _http = new HttpClient(Handler);
        Dispatcher = new WebhookDispatcher(
            url,
            (options ?? new RelayOptions { BatchWindow = TimeSpan.Zero }).Normalize([]),
            _http,
            Logger,
            Time,
            (duration, _) =>
            {
                Delays.Enqueue(duration);
                Time.Advance(duration);
                return Task.CompletedTask;
            });
    }

    public WebhookUrl Url { get; }

    public FakeTimeProvider Time { get; } = new(TestData.Now);

    public FakeHttpHandler Handler { get; }

    public ListLogger Logger { get; } = new();

    public ConcurrentQueue<TimeSpan> Delays { get; } = new();

    public WebhookDispatcher Dispatcher { get; }

    public void Enqueue(int count, Func<int, FormattedMessage>? factory = null)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.True(Dispatcher.TryEnqueue(factory?.Invoke(i) ?? FormattedMessage.FromText($"message {i}")));
        }
    }

    /// <summary>Starts the loop and waits until everything queued so far has been processed.</summary>
    public async Task RunToCompletionAsync()
    {
        Dispatcher.Start();
        await Dispatcher.StopAsync(TimeSpan.FromMinutes(30)).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Dispatcher.DisposeAsync();
        _http.Dispose();
    }
}
