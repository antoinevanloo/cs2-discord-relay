using System.Net;
using System.Text.Json;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Formatting;
using Cs2DiscordRelay.Core.Options;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests.Discord;

public sealed class WebhookDispatcherTests
{
    private static readonly RelayOptions NoBatching = new() { BatchWindow = TimeSpan.Zero };

    private static Func<HttpRequestMessage, HttpResponseMessage> Respond(HttpStatusCode status, string? json = null) =>
        _ => FakeHttpHandler.Status(status, json);

    private static FormattedMessage EmbedMessage(int i) =>
        FormattedMessage.FromEmbed(new DiscordEmbed { Description = $"event {i}" });

    [Fact]
    public async Task Posts_json_to_the_webhook_with_mentions_disabled()
    {
        await using var h = new DispatcherHarness(NoBatching);
        Assert.True(h.Dispatcher.TryEnqueue(FormattedMessage.FromText("@everyone hi")));

        await h.RunToCompletionAsync();

        var request = Assert.Single(h.Handler.RequestList);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(DispatcherHarness.RawUrl, request.Uri.AbsoluteUri);
        Assert.Equal("application/json", request.MediaType);
        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal("@everyone hi", json.RootElement.GetProperty("content").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
    }

    [Fact]
    public async Task Burst_is_sent_as_a_single_request_after_the_batch_window()
    {
        await using var h = new DispatcherHarness(new RelayOptions { BatchWindow = TimeSpan.FromMilliseconds(800) });
        h.Enqueue(3);

        await h.RunToCompletionAsync();

        var request = Assert.Single(h.Handler.RequestList);
        Assert.Contains("message 0\\nmessage 1\\nmessage 2", request.Body, StringComparison.Ordinal);
        Assert.Equal([TimeSpan.FromMilliseconds(800)], h.Delays);
    }

    [Fact]
    public async Task No_delay_when_batching_is_disabled()
    {
        await using var h = new DispatcherHarness(NoBatching);
        h.Enqueue(2);

        await h.RunToCompletionAsync();

        Assert.Empty(h.Delays);
    }

    [Fact]
    public async Task Waits_retry_after_on_429_then_resends_the_same_payload()
    {
        await using var h = new DispatcherHarness(
            NoBatching,
            Respond(HttpStatusCode.TooManyRequests, """{"message":"You are being rate limited.","retry_after":1.5,"global":false}"""),
            _ => FakeHttpHandler.NoContent());
        h.Enqueue(1);

        await h.RunToCompletionAsync();

        var requests = h.Handler.RequestList;
        Assert.Equal(2, requests.Count);
        Assert.Equal(requests[0].Body, requests[1].Body);
        Assert.Equal([TimeSpan.FromMilliseconds(1500)], h.Delays);
        Assert.Empty(h.Logger.Warnings);
    }

    [Fact]
    public async Task Gives_up_after_repeated_429_and_moves_on()
    {
        var responses = Enumerable.Repeat(Respond(HttpStatusCode.TooManyRequests, """{"retry_after":0.1}"""), WebhookDispatcher.MaxRateLimitAttempts);
        await using var h = new DispatcherHarness(NoBatching, [.. responses]);
        h.Enqueue(1);

        await h.RunToCompletionAsync();

        Assert.Equal(WebhookDispatcher.MaxRateLimitAttempts, h.Handler.Requests.Count);
        Assert.Contains(h.Logger.Warnings, w => w.Contains("rate limited", StringComparison.Ordinal));
        Assert.True(h.Dispatcher.IsEnabled);
    }

    [Fact]
    public async Task Waits_for_an_exhausted_bucket_before_the_next_request()
    {
        await using var h = new DispatcherHarness(
            NoBatching,
            _ => FakeHttpHandler.WithHeaders(
                FakeHttpHandler.NoContent(),
                ("X-RateLimit-Remaining", "0"),
                ("X-RateLimit-Reset-After", "2")),
            _ => FakeHttpHandler.NoContent());
        h.Enqueue(DiscordLimits.EmbedsPerMessage + 1, EmbedMessage); // two payloads

        await h.RunToCompletionAsync();

        Assert.Equal(2, h.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(2)], h.Delays);
    }

    [Fact]
    public async Task Retries_server_errors_with_exponential_backoff()
    {
        await using var h = new DispatcherHarness(
            NoBatching,
            Respond(HttpStatusCode.BadGateway),
            Respond(HttpStatusCode.ServiceUnavailable),
            _ => FakeHttpHandler.NoContent());
        h.Enqueue(1);

        await h.RunToCompletionAsync();

        Assert.Equal(3, h.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], h.Delays);
        Assert.Empty(h.Logger.Warnings);
    }

    [Fact]
    public async Task Drops_a_message_after_max_attempts_and_keeps_relaying()
    {
        await using var h = new DispatcherHarness(
            NoBatching,
            Respond(HttpStatusCode.InternalServerError),
            Respond(HttpStatusCode.InternalServerError),
            Respond(HttpStatusCode.InternalServerError),
            _ => FakeHttpHandler.NoContent());
        h.Enqueue(1);
        h.Dispatcher.Start();
        await WaitUntilAsync(() => h.Handler.Requests.Count >= WebhookDispatcher.MaxTransientAttempts);
        h.Enqueue(1);

        await h.RunToCompletionAsync();

        Assert.Equal(WebhookDispatcher.MaxTransientAttempts + 1, h.Handler.Requests.Count);
        Assert.Single(h.Logger.Warnings, w => w.Contains("Could not reach Discord", StringComparison.Ordinal));
        Assert.True(h.Dispatcher.IsEnabled);
    }

    [Fact]
    public async Task Retries_network_errors()
    {
        await using var h = new DispatcherHarness(
            NoBatching,
            _ => throw new HttpRequestException("Connection refused"),
            _ => FakeHttpHandler.NoContent());
        h.Enqueue(1);

        await h.RunToCompletionAsync();

        Assert.Equal(2, h.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(1)], h.Delays);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Disables_itself_when_discord_rejects_the_webhook(HttpStatusCode status)
    {
        await using var h = new DispatcherHarness(NoBatching, Respond(status, """{"message":"Unknown Webhook","code":10015}"""));
        h.Enqueue(DiscordLimits.EmbedsPerMessage * 3, EmbedMessage); // three payloads

        await h.RunToCompletionAsync();

        Assert.Single(h.Handler.Requests);
        Assert.False(h.Dispatcher.IsEnabled);
        Assert.False(h.Dispatcher.TryEnqueue(FormattedMessage.FromText("ignored")));
        var warning = Assert.Single(h.Logger.Warnings);
        Assert.Contains("Relay disabled", warning, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Drops_payloads_refused_by_discord_and_continues()
    {
        await using var h = new DispatcherHarness(
            NoBatching,
            Respond(HttpStatusCode.BadRequest, """{"message":"Invalid Form Body","code":50035}"""),
            _ => FakeHttpHandler.NoContent());
        h.Enqueue(DiscordLimits.EmbedsPerMessage + 1, EmbedMessage);

        await h.RunToCompletionAsync();

        Assert.Equal(2, h.Handler.Requests.Count);
        Assert.Single(h.Logger.Warnings, w => w.Contains("Invalid Form Body", StringComparison.Ordinal));
        Assert.True(h.Dispatcher.IsEnabled);
    }

    [Fact]
    public async Task Drops_the_oldest_messages_when_the_queue_is_full()
    {
        await using var h = new DispatcherHarness(new RelayOptions { BatchWindow = TimeSpan.Zero, MaxQueueSize = RelayOptions.MinQueueSize });
        h.Enqueue(RelayOptions.MinQueueSize + 5);

        await h.RunToCompletionAsync();

        var body = Assert.Single(h.Handler.RequestList).Body;
        Assert.DoesNotContain("message 4\\n", body, StringComparison.Ordinal);
        Assert.Contains("message 5\\n", body, StringComparison.Ordinal);
        Assert.Single(h.Logger.Warnings, w => w.Contains("5 message(s) were dropped", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Never_logs_the_webhook_url_or_token()
    {
        await using var h = new DispatcherHarness(
            NoBatching,
            _ => throw new HttpRequestException($"Failed to POST {DispatcherHarness.RawUrl}"),
            _ => throw new HttpRequestException($"token {DispatcherHarness.Token} refused"),
            _ => throw new HttpRequestException("boom"),
            Respond(HttpStatusCode.BadRequest, $$"""{"message":"bad {{DispatcherHarness.Token}}"}"""),
            Respond(HttpStatusCode.NotFound));
        h.Enqueue(DiscordLimits.EmbedsPerMessage * 3, EmbedMessage);

        await h.RunToCompletionAsync();

        Assert.NotEmpty(h.Logger.Entries);
        Assert.DoesNotContain(DispatcherHarness.Token, h.Logger.AllText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Posts_to_the_forum_thread_when_thread_id_is_set()
    {
        await using var h = new DispatcherHarness($"{DispatcherHarness.RawUrl}?thread_id=111111111111111111", NoBatching);
        h.Enqueue(1);

        await h.RunToCompletionAsync();

        Assert.Equal("?thread_id=111111111111111111", Assert.Single(h.Handler.RequestList).Uri.Query);
    }

    [Fact]
    public async Task Refuses_messages_once_stopped()
    {
        await using var h = new DispatcherHarness(NoBatching);

        await h.RunToCompletionAsync();

        Assert.False(h.Dispatcher.TryEnqueue(FormattedMessage.FromText("late")));
    }

    [Fact]
    public async Task Stop_without_start_completes_immediately()
    {
        await using var h = new DispatcherHarness(NoBatching);

        await h.Dispatcher.StopAsync(TimeSpan.FromSeconds(1)).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Empty(h.Handler.Requests);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
