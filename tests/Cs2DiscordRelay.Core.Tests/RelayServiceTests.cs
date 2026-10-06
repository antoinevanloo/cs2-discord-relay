using System.Text.Json;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Options;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests;

public sealed class RelayServiceTests
{
    [Fact]
    public async Task Publish_filters_formats_and_sends()
    {
        Assert.True(WebhookUrl.TryParse(DispatcherHarness.RawUrl, out var url, out _));
        var handler = new FakeHttpHandler();
        var options = new RelayOptions { BatchWindow = TimeSpan.Zero, Format = MessageFormat.Text }.Normalize([]);
        var service = new RelayService(url, options, new ListLogger(), new HttpClient(handler), timeProvider: null);

        Assert.False(service.Publish(TestData.Chat("!rtv")));
        Assert.True(service.Publish(TestData.Chat("gg")));
        await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.RequestList);
        using var json = JsonDocument.Parse(request.Body);
        Assert.Equal("💬 **s1mple**: gg", json.RootElement.GetProperty("content").GetString());
    }
}
