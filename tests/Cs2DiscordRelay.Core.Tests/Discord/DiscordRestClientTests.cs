using System.Net;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Tests.Fakes;

namespace Cs2DiscordRelay.Core.Tests.Discord;

public sealed class DiscordRestClientTests
{
    private static (DiscordRestClient Client, FakeHttpHandler Handler, List<TimeSpan> Delays) Create(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        Assert.True(BotToken.TryParse(TestData.BotToken, out var token, out _));
        var handler = new FakeHttpHandler(responses);
        var delays = new List<TimeSpan>();
        var client = new DiscordRestClient(new HttpClient(handler), token, delay: (d, _) =>
        {
            delays.Add(d);
            return Task.CompletedTask;
        });
        return (client, handler, delays);
    }

    [Fact]
    public async Task Authenticates_as_the_bot_on_api_v10()
    {
        string? authorization = null;
        var (client, handler, _) = Create(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            return FakeHttpHandler.Status(HttpStatusCode.OK, """{"id":"42","username":"RelayBot","bot":true}""");
        });

        var result = await client.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("RelayBot", result.Value!.Username);
        Assert.Equal("https://discord.com/api/v10/users/@me", Assert.Single(handler.RequestList).Uri.AbsoluteUri);
        Assert.Equal("Bot " + TestData.BotToken, authorization);
    }

    [Fact]
    public async Task Reads_the_webhook_channel_without_sending_the_bot_token()
    {
        Assert.True(WebhookUrl.TryParse(DispatcherHarness.RawUrl + "?thread_id=987654321098765432", out var webhook, out _));
        string? authorization = "unset";
        var (client, handler, _) = Create(request =>
        {
            authorization = request.Headers.Authorization?.ToString();
            return FakeHttpHandler.Status(HttpStatusCode.OK, """{"id":"1","channel_id":"555","token":"x"}""");
        });

        var result = await client.GetWebhookAsync(webhook, TestContext.Current.CancellationToken);

        Assert.Equal("555", result.Value!.ChannelId);
        Assert.Equal(DispatcherHarness.RawUrl, Assert.Single(handler.RequestList).Uri.AbsoluteUri);
        Assert.Null(authorization);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, """{"message":"401: Unauthorized","code":0}""", "HTTP 401: 401: Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, """{"message":"Missing Access","code":50001}""", "HTTP 403: Missing Access")]
    [InlineData(HttpStatusCode.NotFound, """{"message":"Unknown Channel","code":10003}""", "HTTP 404: Unknown Channel")]
    public async Task Reports_client_errors_with_discord_message(HttpStatusCode status, string body, string expected)
    {
        var (client, _, _) = Create(_ => FakeHttpHandler.Status(status, body));

        var result = await client.GetChannelAsync(555, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsTransient);
        Assert.Equal((int)status, result.StatusCode);
        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public async Task Waits_retry_after_on_429()
    {
        var (client, handler, delays) = Create(
            _ => FakeHttpHandler.Status(HttpStatusCode.TooManyRequests, """{"message":"You are being rate limited.","retry_after":1.5,"global":false}"""),
            _ => FakeHttpHandler.Status(HttpStatusCode.OK, """{"url":"wss://gateway.discord.gg"}"""));

        var result = await client.GetGatewayBotAsync(TestContext.Current.CancellationToken);

        Assert.Equal("wss://gateway.discord.gg", result.Value!.Url);
        Assert.Equal(2, handler.RequestList.Count);
        Assert.Equal([TimeSpan.FromSeconds(1.5)], delays);
    }

    [Fact]
    public async Task Network_errors_are_transient_and_redacted()
    {
        var (client, _, _) = Create(_ => throw new HttpRequestException($"boom {TestData.BotToken}"));

        var result = await client.GetCurrentUserAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsTransient);
        Assert.Equal(0, result.StatusCode);
        Assert.DoesNotContain(TestData.BotToken, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void User_agent_follows_the_discord_format() =>
        Assert.Matches(@"^DiscordBot \(https://github\.com/antoinevanloo/cs2-discord-relay, \d+\.\d+\.\d+[^)]*\)$", DiscordHttp.UserAgent);

    [Fact]
    public void Http_client_sends_the_user_agent()
    {
        using var http = DiscordHttp.CreateClient();

        Assert.Equal(DiscordHttp.UserAgent, http.DefaultRequestHeaders.UserAgent.ToString());
    }
}
