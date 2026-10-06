using Cs2DiscordRelay.Core.Discord;

namespace Cs2DiscordRelay.Core.Formatting;

/// <summary>One rendered event: either a text line or an embed, never both.</summary>
public sealed record FormattedMessage
{
    private FormattedMessage(string? text, DiscordEmbed? embed)
    {
        Text = text;
        Embed = embed;
    }

    /// <summary>Markdown text line, when rendered as text.</summary>
    public string? Text { get; }

    /// <summary>Embed, when rendered as an embed.</summary>
    public DiscordEmbed? Embed { get; }

    /// <summary>Creates a text message.</summary>
    /// <param name="text">Already escaped markdown text.</param>
    /// <returns>The message.</returns>
    public static FormattedMessage FromText(string text) => new(text ?? throw new ArgumentNullException(nameof(text)), null);

    /// <summary>Creates an embed message.</summary>
    /// <param name="embed">The embed.</param>
    /// <returns>The message.</returns>
    public static FormattedMessage FromEmbed(DiscordEmbed embed) => new(null, embed ?? throw new ArgumentNullException(nameof(embed)));
}
