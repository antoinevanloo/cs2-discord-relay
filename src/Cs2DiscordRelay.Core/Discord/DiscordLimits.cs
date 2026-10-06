namespace Cs2DiscordRelay.Core.Discord;

/// <summary>Hard limits enforced by the Discord API on webhook messages.</summary>
/// <remarks>See https://discord.com/developers/docs/resources/message#embed-object-embed-limits.</remarks>
public static class DiscordLimits
{
    /// <summary>Maximum length of <c>content</c>.</summary>
    public const int ContentLength = 2000;

    /// <summary>Maximum number of embeds per message.</summary>
    public const int EmbedsPerMessage = 10;

    /// <summary>Maximum combined length of all embed texts in one message.</summary>
    public const int EmbedsTotalLength = 6000;

    /// <summary>Maximum length of an embed title.</summary>
    public const int EmbedTitleLength = 256;

    /// <summary>Maximum length of an embed description.</summary>
    public const int EmbedDescriptionLength = 4096;

    /// <summary>Maximum length of an embed author name.</summary>
    public const int EmbedAuthorNameLength = 256;

    /// <summary>Maximum length of an embed footer.</summary>
    public const int EmbedFooterLength = 2048;
}
