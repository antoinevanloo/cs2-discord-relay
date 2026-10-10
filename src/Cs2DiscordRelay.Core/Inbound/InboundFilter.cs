namespace Cs2DiscordRelay.Core.Inbound;

/// <summary>Decides which Discord messages reach the game chat.</summary>
internal static class InboundFilter
{
    /// <summary>
    /// Keeps regular messages and replies posted by people in <paramref name="channelId"/>. Bots and webhooks
    /// (including the relay's own posts) are ignored, which prevents echo loops.
    /// </summary>
    public static bool ShouldRelay(DiscordMessage message, ulong channelId, ulong botUserId, InboundOptions options) =>
        message.ChannelId == channelId
        && DiscordMessage.RelayedTypes.Contains(message.Type)
        && !message.IsBot
        && !message.IsWebhook
        && message.AuthorId != botUserId
        && (options.AllowedRoleIds.Count == 0 || message.RoleIds.Any(options.AllowedRoleIds.Contains));
}
