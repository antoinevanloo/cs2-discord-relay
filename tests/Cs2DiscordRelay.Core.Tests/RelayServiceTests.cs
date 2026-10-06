using System.Net;
using System.Text.Json;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Options;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests;

public sealed class RelayServiceTests
{
    private static RelayService CreateService(FakeHttpHandler handler, RelayOptions options)
    {
        Assert.True(WebhookUrl.TryParse(DispatcherHarness.RawUrl, out var url, out _));
        return new RelayService(url, options.Normalize([]), new ListLogger(), new HttpClient(handler), timeProvider: null);
    }

    [Fact]
    public async Task Publish_filters_formats_and_sends()
    {
        var handler = new FakeHttpHandler();
        var service = CreateService(handler, new RelayOptions { BatchWindow = TimeSpan.Zero, Format = MessageFormat.Text });

        Assert.False(service.Publish(TestData.Chat("!rtv")));
        Assert.True(service.Publish(TestData.Chat("gg")));
        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.RequestList);
        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal("💬 **s1mple**: gg", json.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Status_lists_the_format_and_enabled_events()
    {
        await using var service = CreateService(new FakeHttpHandler(), new RelayOptions
        {
            Events = new EventToggles { TeamChat = false, MapChange = false },
        });

        Assert.Equal(
            "Running: Embed format, relaying chat, connections, disconnections. 0 message(s) waiting to be sent.",
            service.DescribeStatus());
    }

    [Fact]
    public async Task Status_reports_when_every_event_is_disabled()
    {
        await using var service = CreateService(new FakeHttpHandler(), new RelayOptions
        {
            Events = new EventToggles { Chat = false, TeamChat = false, PlayerConnect = false, PlayerDisconnect = false, MapChange = false },
        });

        Assert.Contains("relaying nothing (every event is disabled)", service.DescribeStatus(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Status_reports_a_rejected_webhook_without_leaking_it()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Status(HttpStatusCode.NotFound, """{"message":"Unknown Webhook","code":10015}"""));
        var service = CreateService(handler, new RelayOptions { BatchWindow = TimeSpan.Zero });

        Assert.True(service.Publish(TestData.Test()));
        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(service.IsEnabled);
        Assert.StartsWith("Stopped: Discord rejected the webhook", service.DescribeStatus(), StringComparison.Ordinal);
        Assert.DoesNotContain(DispatcherHarness.Token, service.DescribeStatus(), StringComparison.Ordinal);
    }
}
