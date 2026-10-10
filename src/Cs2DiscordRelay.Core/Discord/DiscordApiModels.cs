using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cs2DiscordRelay.Core.Discord;

// Subsets of Discord API objects used by the Discord → game bridge.
// See https://discord.com/developers/docs/resources/user, /resources/channel, /resources/message
// and /events/gateway-events. Snowflakes are strings on the wire.

/// <summary>A Discord user.</summary>
internal sealed record DiscordUser
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("global_name")]
    public string? GlobalName { get; init; }

    [JsonPropertyName("bot")]
    public bool? Bot { get; init; }

    [JsonPropertyName("member")]
    public GuildMember? Member { get; init; }
}

/// <summary>Partial guild member, as sent with messages.</summary>
internal sealed record GuildMember
{
    [JsonPropertyName("nick")]
    public string? Nick { get; init; }

    [JsonPropertyName("roles")]
    public IReadOnlyList<string>? Roles { get; init; }
}

/// <summary>A Discord channel.</summary>
internal sealed record DiscordChannel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("guild_id")]
    public string? GuildId { get; init; }
}

/// <summary>Response of <c>GET /gateway/bot</c>.</summary>
internal sealed record GatewayBotResponse
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

/// <summary>Response of <c>GET /webhooks/{id}/{token}</c>.</summary>
internal sealed record WebhookInfo
{
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; init; }
}

/// <summary>Gateway envelope: <c>{ op, d, s, t }</c>.</summary>
internal sealed record GatewayPayload
{
    [JsonPropertyName("op")]
    public int Op { get; init; }

    [JsonPropertyName("d")]
    public JsonElement? Data { get; init; }

    [JsonPropertyName("s")]
    public int? Sequence { get; init; }

    [JsonPropertyName("t")]
    public string? EventName { get; init; }
}

/// <summary>Data of the Hello (op 10) payload.</summary>
internal sealed record HelloData
{
    [JsonPropertyName("heartbeat_interval")]
    public int HeartbeatInterval { get; init; }
}

/// <summary>Data of the READY dispatch.</summary>
internal sealed record ReadyData
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    [JsonPropertyName("resume_gateway_url")]
    public string? ResumeGatewayUrl { get; init; }

    [JsonPropertyName("user")]
    public DiscordUser? User { get; init; }
}

/// <summary>Data of the MESSAGE_CREATE dispatch.</summary>
internal sealed record GatewayMessage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("webhook_id")]
    public string? WebhookId { get; init; }

    [JsonPropertyName("author")]
    public DiscordUser? Author { get; init; }

    [JsonPropertyName("member")]
    public GuildMember? Member { get; init; }

    [JsonPropertyName("mentions")]
    public IReadOnlyList<DiscordUser>? Mentions { get; init; }

    [JsonPropertyName("attachments")]
    public IReadOnlyList<MessageAttachment>? Attachments { get; init; }

    [JsonPropertyName("sticker_items")]
    public IReadOnlyList<StickerItem>? StickerItems { get; init; }
}

/// <summary>Message attachment.</summary>
internal sealed record MessageAttachment
{
    [JsonPropertyName("content_type")]
    public string? ContentType { get; init; }
}

/// <summary>Sticker sent with a message.</summary>
internal sealed record StickerItem
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>Identify (op 2) payload.</summary>
internal sealed record IdentifyPayload([property: JsonPropertyName("d")] IdentifyData Data)
{
    [JsonPropertyName("op")]
    public int Op { get; init; } = GatewayOpcodes.Identify;
}

internal sealed record IdentifyData(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("intents")] int Intents,
    [property: JsonPropertyName("properties")] IdentifyProperties Properties);

internal sealed record IdentifyProperties(
    [property: JsonPropertyName("os")] string Os,
    [property: JsonPropertyName("browser")] string Browser,
    [property: JsonPropertyName("device")] string Device);

/// <summary>Resume (op 6) payload.</summary>
internal sealed record ResumePayload([property: JsonPropertyName("d")] ResumeData Data)
{
    [JsonPropertyName("op")]
    public int Op { get; init; } = GatewayOpcodes.Resume;
}

internal sealed record ResumeData(
    [property: JsonPropertyName("token")] string Token,
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("seq")] int? Sequence);

/// <summary>Source-generated serializer for the bridge (no reflection at runtime).</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DiscordUser))]
[JsonSerializable(typeof(DiscordChannel))]
[JsonSerializable(typeof(GatewayBotResponse))]
[JsonSerializable(typeof(WebhookInfo))]
[JsonSerializable(typeof(GatewayPayload))]
[JsonSerializable(typeof(HelloData))]
[JsonSerializable(typeof(ReadyData))]
[JsonSerializable(typeof(GatewayMessage))]
[JsonSerializable(typeof(IdentifyPayload))]
[JsonSerializable(typeof(ResumePayload))]
[JsonSerializable(typeof(bool))]
internal sealed partial class DiscordApiJsonContext : JsonSerializerContext;
