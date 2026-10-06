using System.Net;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Cs2DiscordRelay.Core.Tests.Discord;

public sealed class RateLimitPolicyTests
{
    private static readonly FakeTimeProvider Time = new(TestData.Now);

    private static HttpResponseMessage TooMany(params (string, string)[] headers) =>
        FakeHttpHandler.WithHeaders(new HttpResponseMessage(HttpStatusCode.TooManyRequests), headers);

    [Fact]
    public void Body_retry_after_wins_and_keeps_millisecond_precision()
    {
        using var response = TooMany(("Retry-After", "2"));

        var delay = RateLimitPolicy.GetRetryAfter(response.Headers, """{"retry_after":1.234}""", Time);

        Assert.Equal(TimeSpan.FromMilliseconds(1234), delay);
    }

    [Theory]
    [InlineData("3", 3000)]
    [InlineData("2.5", 2500)]
    public void Retry_after_header_is_used_without_body(string header, int expectedMs)
    {
        using var response = TooMany(("Retry-After", header));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), RateLimitPolicy.GetRetryAfter(response.Headers, null, Time));
    }

    [Fact]
    public void Reset_after_header_is_the_last_resort()
    {
        using var response = TooMany(("X-RateLimit-Reset-After", "0.75"));

        Assert.Equal(TimeSpan.FromMilliseconds(750), RateLimitPolicy.GetRetryAfter(response.Headers, "not json", Time));
    }

    [Fact]
    public void Defaults_to_one_second_without_any_hint()
    {
        using var response = TooMany();

        Assert.Equal(RateLimitPolicy.DefaultRetryAfter, RateLimitPolicy.GetRetryAfter(response.Headers, null, Time));
    }

    [Fact]
    public void Absurd_retry_after_is_capped()
    {
        using var response = TooMany();

        Assert.Equal(RateLimitPolicy.MaxRetryAfter, RateLimitPolicy.GetRetryAfter(response.Headers, """{"retry_after":99999}""", Time));
    }

    [Theory]
    [InlineData("0", "1.5", 1500)]
    [InlineData("1", "1.5", null)]
    [InlineData("4", "1.5", null)]
    public void Bucket_wait_only_when_remaining_is_zero(string remaining, string resetAfter, int? expectedMs)
    {
        using var response = FakeHttpHandler.WithHeaders(
            new HttpResponseMessage(HttpStatusCode.NoContent),
            ("X-RateLimit-Remaining", remaining),
            ("X-RateLimit-Reset-After", resetAfter));

        var wait = RateLimitPolicy.GetBucketWait(response.Headers);

        Assert.Equal(expectedMs is null ? null : TimeSpan.FromMilliseconds(expectedMs.Value), wait);
    }

    [Fact]
    public void No_bucket_headers_means_no_wait()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        Assert.Null(RateLimitPolicy.GetBucketWait(response.Headers));
    }

    [Fact]
    public void Backoff_is_exponential_and_capped() =>
        Assert.Equal(
            [1, 2, 4, 8, 16, 30, 30],
            Enumerable.Range(1, 7).Select(a => RateLimitPolicy.GetBackoff(a).TotalSeconds));
}
