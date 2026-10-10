using System.Globalization;
using Cs2DiscordRelay.Core.Discord;

namespace Cs2DiscordRelay.Core.Inbound;

/// <summary>A Discord message reduced to what the bridge needs, decoupled from the wire format.</summary>
internal sealed record DiscordMessage(
    ulong ChannelId,
    int Type,
    string Content,
    ulong AuthorId,
    string AuthorName,
    bool IsBot,
    bool IsWebhook,
    IReadOnlyList<ulong> RoleIds,
    IReadOnlyDictionary<ulong, string> MentionNames,
    int ImageCount,
    int FileCount,
    IReadOnlyList<string> StickerNames)
{
    /// <summary>Message types relayed to the game: DEFAULT and REPLY.</summary>
    public static readonly IReadOnlySet<int> RelayedTypes = new HashSet<int> { 0, 19 };

    /// <summary>Maps a MESSAGE_CREATE payload; <see langword="null"/> when its ids are malformed.</summary>
    public static DiscordMessage? From(GatewayMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!Snowflake.TryParse(message.ChannelId, out var channelId) || !Snowflake.TryParse(message.Author?.Id, out var authorId))
        {
            return null;
        }

        var mentions = new Dictionary<ulong, string>();
        foreach (var user in message.Mentions ?? [])
        {
            if (Snowflake.TryParse(user.Id, out var id))
            {
                mentions[id] = DisplayName(user.Member, user);
            }
        }

        var attachments = message.Attachments ?? [];
        var images = attachments.Count(a => a.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true);

        return new DiscordMessage(
            channelId,
            message.Type,
            message.Content ?? string.Empty,
            authorId,
            DisplayName(message.Member, message.Author),
            message.Author?.Bot == true,
            !string.IsNullOrEmpty(message.WebhookId),
            [.. (message.Member?.Roles ?? []).Select(r => Snowflake.TryParse(r, out var role) ? role : 0).Where(r => r != 0)],
            mentions,
            images,
            attachments.Count - images,
            [.. (message.StickerItems ?? []).Select(s => s.Name).OfType<string>()]);
    }

    // Server nickname, then global display name, then username (Discord client convention).
    private static string DisplayName(GuildMember? member, DiscordUser? user) =>
        FirstNonBlank(member?.Nick, user?.GlobalName, user?.Username) ?? "unknown";

    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}

/// <summary>Discord ids (snowflakes) travel as strings.</summary>
public static class Snowflake
{
    /// <summary>Parses a snowflake id.</summary>
    /// <param name="value">The id, e.g. <c>"175928847299117063"</c>.</param>
    /// <param name="id">The parsed id.</param>
    /// <returns><see langword="true"/> for a non-zero numeric id.</returns>
    public static bool TryParse(string? value, out ulong id) =>
        ulong.TryParse(value?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0;
}
