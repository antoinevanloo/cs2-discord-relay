using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Cs2DiscordRelay.Core.Formatting;

namespace Cs2DiscordRelay.Core.Inbound;

/// <summary>A Discord message ready for the game chat: plain text, single line, no control characters.</summary>
/// <param name="Name">Author display name.</param>
/// <param name="Message">Message text.</param>
public sealed record GameChatLine(string Name, string Message);

/// <summary>Turns Discord markup into plain text safe for the CS2 chat.</summary>
/// <remarks>
/// CS2 uses control characters (<c>\x01</c>–<c>\x10</c>) as color codes, so they are always stripped from
/// Discord text: nobody can recolor the chat or fake a server message.
/// </remarks>
internal static partial class GameChatFormatter
{
    /// <summary>Converts a message, or returns <see langword="null"/> when nothing is left to show.</summary>
    public static GameChatLine? Format(DiscordMessage message, InboundOptions options)
    {
        var text = ToPlainText(message.Content, message.MentionNames);
        var extras = Extras(message);
        if (extras.Length > 0)
        {
            text = text.Length == 0 ? extras : text + " " + extras;
        }

        if (text.Length == 0)
        {
            return null;
        }

        var name = DiscordMarkdown.Sanitize(message.AuthorName);
        return new GameChatLine(
            DiscordMarkdown.Truncate(name.Length == 0 ? "unknown" : name, InboundOptions.MaxNameLength),
            DiscordMarkdown.Truncate(text, options.MaxMessageLength));
    }

    /// <summary>
    /// Fills <c>{Name}</c> and <c>{Message}</c> in one pass, so a name containing <c>{Message}</c> is never expanded.
    /// Color tags must already be replaced: the inserted text is never interpreted.
    /// </summary>
    public static string Render(string template, GameChatLine line) =>
        Placeholder().Replace(template, match =>
            match.Value.Equals("{Name}", StringComparison.OrdinalIgnoreCase) ? line.Name : line.Message);

    internal static string ToPlainText(string content, IReadOnlyDictionary<ulong, string> mentionNames)
    {
        var text = CodeBlock().Replace(content, m => m.Groups["code"].Value);
        text = InlineCode().Replace(text, m => m.Groups["code"].Value);
        text = Spoiler().Replace(text, "[spoiler]");
        text = MaskedLink().Replace(text, m => m.Groups["text"].Value);
        text = AngleLink().Replace(text, m => m.Groups["url"].Value);
        text = UserMention().Replace(text, m =>
            "@" + (ulong.TryParse(m.Groups["id"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                   && mentionNames.TryGetValue(id, out var name) ? name : "user"));
        text = RoleMention().Replace(text, "@role");
        text = ChannelMention().Replace(text, "#channel");
        text = CustomEmoji().Replace(text, m => ":" + m.Groups["name"].Value + ":");
        text = SlashCommand().Replace(text, m => "/" + m.Groups["name"].Value);
        text = Timestamp().Replace(text, m =>
            long.TryParse(m.Groups["ts"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seconds)
            && seconds is > -62135596800 and < 253402300799
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                : m.Value);
        text = Emphasis().Replace(text, m => m.Groups["text"].Value);
        text = LinePrefix().Replace(text, string.Empty);
        text = MarkdownEscape().Replace(text, m => m.Groups["c"].Value);
        return DiscordMarkdown.Sanitize(text);
    }

    private static string Extras(DiscordMessage message)
    {
        var parts = new StringBuilder();
        Append(parts, message.ImageCount, "[image]", "images");
        Append(parts, message.FileCount, "[file]", "files");
        foreach (var sticker in message.StickerNames)
        {
            parts.Append(parts.Length > 0 ? " " : string.Empty).Append("[sticker: ").Append(DiscordMarkdown.Sanitize(sticker)).Append(']');
        }

        return parts.ToString();

        static void Append(StringBuilder parts, int count, string single, string plural)
        {
            if (count > 0)
            {
                parts.Append(parts.Length > 0 ? " " : string.Empty)
                    .Append(count == 1 ? single : string.Create(CultureInfo.InvariantCulture, $"[{count} {plural}]"));
            }
        }
    }

    [GeneratedRegex(@"\{(?:Name|Message)\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"```(?:[A-Za-z0-9+#-]*\n)?(?<code>[\s\S]*?)```", RegexOptions.CultureInvariant)]
    private static partial Regex CodeBlock();

    [GeneratedRegex(@"`(?<code>[^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"\|\|[\s\S]+?\|\|", RegexOptions.CultureInvariant)]
    private static partial Regex Spoiler();

    [GeneratedRegex(@"\[(?<text>[^\]\n]+)\]\(<?https?://[^)\s]+>?\)", RegexOptions.CultureInvariant)]
    private static partial Regex MaskedLink();

    [GeneratedRegex(@"<(?<url>https?://[^>\s]+)>", RegexOptions.CultureInvariant)]
    private static partial Regex AngleLink();

    [GeneratedRegex(@"<@!?(?<id>\d+)>", RegexOptions.CultureInvariant)]
    private static partial Regex UserMention();

    [GeneratedRegex(@"<@&\d+>", RegexOptions.CultureInvariant)]
    private static partial Regex RoleMention();

    [GeneratedRegex(@"<#\d+>", RegexOptions.CultureInvariant)]
    private static partial Regex ChannelMention();

    [GeneratedRegex(@"<a?:(?<name>\w+):\d+>", RegexOptions.CultureInvariant)]
    private static partial Regex CustomEmoji();

    [GeneratedRegex(@"</(?<name>[\w -]+):\d+>", RegexOptions.CultureInvariant)]
    private static partial Regex SlashCommand();

    [GeneratedRegex(@"<t:(?<ts>-?\d+)(?::[tTdDfFsSR])?>", RegexOptions.CultureInvariant)]
    private static partial Regex Timestamp();

    [GeneratedRegex(@"(\*\*|__|~~)(?<text>.+?)\1", RegexOptions.CultureInvariant)]
    private static partial Regex Emphasis();

    [GeneratedRegex(@"^(?:#{1,3} |-# |>>> |> )", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex LinePrefix();

    [GeneratedRegex(@"\\(?<c>[\\*_~`|<>#\-\[\]()@:.+!])", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownEscape();
}
