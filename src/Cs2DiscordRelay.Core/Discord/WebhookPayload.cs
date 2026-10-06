using System.Text.Json.Serialization;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>Body of an "Execute Webhook" request.</summary>
/// <remarks>See https://discord.com/developers/docs/resources/webhook#execute-webhook.</remarks>
public sealed record WebhookPayload
{
    /// <summary>Message flag that hides link previews.</summary>
    public const int SuppressEmbedsFlag = 1 << 2;

    /// <summary>Plain text content.</summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>Overrides the webhook's display name.</summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>Overrides the webhook's avatar.</summary>
    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    /// <summary>Rich embeds (up to 10).</summary>
    [JsonPropertyName("embeds")]
    public IReadOnlyList<DiscordEmbed>? Embeds { get; init; }

    /// <summary>Always empty: nobody can be pinged from the game, whatever the text contains.</summary>
    [JsonPropertyName("allowed_mentions")]
    public AllowedMentions AllowedMentions { get; init; } = AllowedMentions.None;

    /// <summary>Message flags, e.g. <see cref="SuppressEmbedsFlag"/>.</summary>
    [JsonPropertyName("flags")]
    public int? Flags { get; init; }
}

/// <summary>Controls which mentions in the message actually notify users.</summary>
public sealed record AllowedMentions
{
    /// <summary>Disables every mention type, including <c>@everyone</c> and <c>@here</c>.</summary>
    public static readonly AllowedMentions None = new();

    /// <summary>Mention types to parse. Empty means none.</summary>
    [JsonPropertyName("parse")]
    public IReadOnlyList<string> Parse { get; init; } = [];
}

/// <summary>A Discord rich embed.</summary>
public sealed record DiscordEmbed
{
    /// <summary>Title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Main text (markdown).</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Accent color as a 24-bit integer.</summary>
    [JsonPropertyName("color")]
    public int? Color { get; init; }

    /// <summary>ISO 8601 timestamp shown next to the footer.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Author line.</summary>
    [JsonPropertyName("author")]
    public EmbedAuthor? Author { get; init; }

    /// <summary>Footer line.</summary>
    [JsonPropertyName("footer")]
    public EmbedFooter? Footer { get; init; }

    /// <summary>Number of characters Discord counts towards <see cref="DiscordLimits.EmbedsTotalLength"/>.</summary>
    [JsonIgnore]
    public int TextLength =>
        (Title?.Length ?? 0) + (Description?.Length ?? 0) + (Author?.Name.Length ?? 0) + (Footer?.Text.Length ?? 0);
}

/// <summary>Embed author line. Rendered as plain text, markdown is not interpreted.</summary>
/// <param name="Name">Author name.</param>
public sealed record EmbedAuthor([property: JsonPropertyName("name")] string Name)
{
    /// <summary>Link opened when clicking the name.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

/// <summary>Embed footer. Rendered as plain text, markdown is not interpreted.</summary>
/// <param name="Text">Footer text.</param>
public sealed record EmbedFooter([property: JsonPropertyName("text")] string Text);

/// <summary>Source-generated serializer for webhook payloads (no reflection at runtime).</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WebhookPayload))]
[JsonSerializable(typeof(DiscordErrorResponse))]
internal sealed partial class WebhookJsonContext : JsonSerializerContext;

/// <summary>Subset of the JSON body Discord returns with errors and 429 responses.</summary>
internal sealed record DiscordErrorResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("code")]
    public int? Code { get; init; }

    [JsonPropertyName("retry_after")]
    public double? RetryAfter { get; init; }

    [JsonPropertyName("global")]
    public bool? Global { get; init; }
}

/// <summary>Serialization helpers for <see cref="WebhookPayload"/>.</summary>
internal static class WebhookJson
{
    public static string Serialize(WebhookPayload payload) =>
        System.Text.Json.JsonSerializer.Serialize(payload, WebhookJsonContext.Default.WebhookPayload);

    public static DiscordErrorResponse? TryParseError(string body)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(body, WebhookJsonContext.Default.DiscordErrorResponse);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
