using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>
/// A validated Discord webhook URL. The URL embeds a secret token, so it is never exposed
/// through <see cref="ToString"/>, logs or error messages.
/// </summary>
public sealed partial class WebhookUrl
{
    /// <summary>What is shown instead of the real URL.</summary>
    public const string Redacted = "https://discord.com/api/webhooks/***";

    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord.com", "ptb.discord.com", "canary.discord.com",
        "discordapp.com", "ptb.discordapp.com", "canary.discordapp.com",
    };

    private readonly string _raw;
    private readonly string _token;

    private WebhookUrl(Uri uri, string raw, string token, ulong? threadId)
    {
        Uri = uri;
        _raw = raw;
        _token = token;
        ThreadId = threadId;
    }

    /// <summary>The endpoint to POST to. Internal so it cannot leak through the public API.</summary>
    internal Uri Uri { get; }

    /// <summary>The webhook endpoint without query string, for "Get Webhook with Token".</summary>
    internal Uri BaseUri => new(Uri.GetLeftPart(UriPartial.Path));

    /// <summary>Forum thread the webhook posts into (<c>?thread_id=</c>), if any.</summary>
    public ulong? ThreadId { get; }

    /// <summary>Validates a webhook URL copied from Discord.</summary>
    /// <param name="value">The URL, e.g. <c>https://discord.com/api/webhooks/{id}/{token}</c>.</param>
    /// <param name="url">The parsed URL on success.</param>
    /// <param name="error">Why the URL was rejected. Never contains the URL itself.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a valid webhook URL.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out WebhookUrl? url, [NotNullWhen(false)] out string? error)
    {
        url = null;
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            error = "WebhookUrl is empty.";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "WebhookUrl is not a valid absolute URL.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "WebhookUrl must use https.";
            return false;
        }

        if (!AllowedHosts.Contains(uri.Host) || !uri.IsDefaultPort)
        {
            error = "WebhookUrl must point to discord.com.";
            return false;
        }

        var match = WebhookPath().Match(uri.AbsolutePath);
        if (!match.Success)
        {
            error = "WebhookUrl does not look like https://discord.com/api/webhooks/<id>/<token>.";
            return false;
        }

        // Only thread_id is meaningful for webhook execution (posting into a forum thread).
        var threadId = ThreadIdQuery().Match(uri.Query);
        var builder = new UriBuilder(uri) { Query = threadId.Success ? $"thread_id={threadId.Groups["id"].Value}" : string.Empty };

        url = new WebhookUrl(
            builder.Uri,
            trimmed,
            match.Groups["token"].Value,
            threadId.Success ? ulong.Parse(threadId.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture) : null);
        error = null;
        return true;
    }

    /// <summary>Replaces any occurrence of this URL or its token in <paramref name="text"/>.</summary>
    /// <param name="text">Text that may contain the URL, such as an exception message.</param>
    /// <returns>The text without secrets.</returns>
    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace(Uri.AbsoluteUri, Redacted, StringComparison.OrdinalIgnoreCase)
            .Replace(_raw, Redacted, StringComparison.OrdinalIgnoreCase)
            .Replace(_token, "***", StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString() => Redacted;

    [GeneratedRegex(@"^/api/(?:v\d+/)?webhooks/(?<id>\d{15,25})/(?<token>[A-Za-z0-9_\-]{20,})/?$", RegexOptions.CultureInvariant)]
    private static partial Regex WebhookPath();

    [GeneratedRegex(@"(?:^\?|&)thread_id=(?<id>\d{15,25})(?:&|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ThreadIdQuery();
}
