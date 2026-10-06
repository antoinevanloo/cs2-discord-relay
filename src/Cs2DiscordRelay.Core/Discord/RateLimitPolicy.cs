using System.Globalization;
using System.Net.Http.Headers;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>Reads Discord's rate limit signals and computes retry delays.</summary>
/// <remarks>See https://discord.com/developers/docs/topics/rate-limits.</remarks>
internal static class RateLimitPolicy
{
    public static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxRetryAfter = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay requested by a 429 response: the JSON <c>retry_after</c> (millisecond precision) first,
    /// then the <c>Retry-After</c> header, then <c>X-RateLimit-Reset-After</c>.
    /// </summary>
    public static TimeSpan GetRetryAfter(HttpResponseHeaders headers, string? body, TimeProvider time)
    {
        if (body is not null && WebhookJson.TryParseError(body)?.RetryAfter is { } fromBody && fromBody >= 0)
        {
            return Clamp(TimeSpan.FromSeconds(fromBody));
        }

        if (headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta)
            {
                return Clamp(delta);
            }

            if (retryAfter.Date is { } date)
            {
                return Clamp(date - time.GetUtcNow());
            }
        }

        // Discord sends Retry-After as a decimal number of seconds, which the typed header may not parse.
        if (TryGetSeconds(headers, "Retry-After") is { } seconds)
        {
            return Clamp(seconds);
        }

        return TryGetSeconds(headers, "X-RateLimit-Reset-After") is { } resetAfter
            ? Clamp(resetAfter)
            : DefaultRetryAfter;
    }

    /// <summary>
    /// When the bucket is exhausted (<c>X-RateLimit-Remaining: 0</c>), how long to wait before the
    /// next request; otherwise <see langword="null"/>.
    /// </summary>
    public static TimeSpan? GetBucketWait(HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("X-RateLimit-Remaining", out var values)
            || !int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var remaining)
            || remaining > 0)
        {
            return null;
        }

        return TryGetSeconds(headers, "X-RateLimit-Reset-After") is { } resetAfter ? Clamp(resetAfter) : null;
    }

    /// <summary>Exponential backoff for transient failures: 1s, 2s, 4s... capped at <see cref="MaxBackoff"/>.</summary>
    public static TimeSpan GetBackoff(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        var seconds = Math.Pow(2, Math.Min(attempt - 1, 10));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBackoff.TotalSeconds));
    }

    private static TimeSpan? TryGetSeconds(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values)
        && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
        && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static TimeSpan Clamp(TimeSpan value) =>
        value < TimeSpan.Zero ? TimeSpan.Zero : value > MaxRetryAfter ? MaxRetryAfter : value;
}
