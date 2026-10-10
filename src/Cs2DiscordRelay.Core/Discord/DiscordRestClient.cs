using System.Globalization;
using System.Net;
using System.Text.Json.Serialization.Metadata;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>Outcome of a REST call. <see cref="StatusCode"/> is 0 when Discord could not be reached.</summary>
/// <typeparam name="T">Response type.</typeparam>
/// <param name="Value">The response on success.</param>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="Error">What went wrong, without secrets.</param>
internal readonly record struct DiscordRestResult<T>(T? Value, int StatusCode, string? Error)
    where T : class
{
    public bool IsSuccess => Value is not null;

    /// <summary>Network error or 5xx: worth retrying later.</summary>
    public bool IsTransient => StatusCode is 0 or >= 500;
}

/// <summary>The few Discord REST endpoints the bridge needs, authenticated as the bot.</summary>
internal sealed class DiscordRestClient
{
    public const int MaxRateLimitAttempts = 3;

    public static readonly Uri ApiBase = new("https://discord.com/api/v10/");

    private readonly HttpClient _http;
    private readonly BotToken _token;
    private readonly TimeProvider _time;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public DiscordRestClient(HttpClient http, BotToken token, TimeProvider? timeProvider = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _http = http;
        _token = token;
        _time = timeProvider ?? TimeProvider.System;
        _delay = delay ?? ((duration, cancellationToken) => Task.Delay(duration, _time, cancellationToken));
    }

    /// <summary><c>GET /users/@me</c>: validates the token and returns the bot user.</summary>
    public Task<DiscordRestResult<DiscordUser>> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        GetAsync(new Uri(ApiBase, "users/@me"), authenticated: true, DiscordApiJsonContext.Default.DiscordUser, cancellationToken);

    /// <summary><c>GET /channels/{id}</c>: fails with 403/404 when the bot cannot see the channel.</summary>
    public Task<DiscordRestResult<DiscordChannel>> GetChannelAsync(ulong channelId, CancellationToken cancellationToken) =>
        GetAsync(
            new Uri(ApiBase, string.Create(CultureInfo.InvariantCulture, $"channels/{channelId}")),
            authenticated: true,
            DiscordApiJsonContext.Default.DiscordChannel,
            cancellationToken);

    /// <summary><c>GET /gateway/bot</c>: the WebSocket URL to connect to.</summary>
    public Task<DiscordRestResult<GatewayBotResponse>> GetGatewayBotAsync(CancellationToken cancellationToken) =>
        GetAsync(new Uri(ApiBase, "gateway/bot"), authenticated: true, DiscordApiJsonContext.Default.GatewayBotResponse, cancellationToken);

    /// <summary><c>GET /webhooks/{id}/{token}</c> (no authentication): the channel the webhook posts to.</summary>
    public Task<DiscordRestResult<WebhookInfo>> GetWebhookAsync(WebhookUrl webhook, CancellationToken cancellationToken) =>
        GetAsync(webhook.BaseUri, authenticated: false, DiscordApiJsonContext.Default.WebhookInfo, cancellationToken, webhook);

    private async Task<DiscordRestResult<T>> GetAsync<T>(
        Uri uri,
        bool authenticated,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken,
        WebhookUrl? webhook = null)
        where T : class
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (authenticated)
            {
                request.Headers.TryAddWithoutValidation("Authorization", _token.AuthorizationHeader);
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                return new(null, 0, Redact(ex.GetType().Name + ": " + ex.Message, webhook));
            }

            using (response)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var status = (int)response.StatusCode;

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < MaxRateLimitAttempts)
                {
                    await _delay(RateLimitPolicy.GetRetryAfter(response.Headers, body, _time), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var message = WebhookJson.TryParseError(body)?.Message ?? response.ReasonPhrase ?? "unknown error";
                    return new(null, status, Redact(string.Create(CultureInfo.InvariantCulture, $"HTTP {status}: {message}"), webhook));
                }

                try
                {
                    var value = System.Text.Json.JsonSerializer.Deserialize(body, typeInfo);
                    return value is null ? new(null, status, "Empty response from Discord.") : new(value, status, null);
                }
                catch (System.Text.Json.JsonException)
                {
                    return new(null, status, "Unexpected response from Discord.");
                }
            }
        }
    }

    private string Redact(string text, WebhookUrl? webhook) => _token.Redact(webhook?.Redact(text) ?? text);
}
