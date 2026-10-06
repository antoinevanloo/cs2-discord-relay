using System.Globalization;
using System.Text;

namespace Cs2DiscordRelay.Core.Formatting;

/// <summary>
/// Makes untrusted player input safe to display in Discord.
/// </summary>
/// <remarks>
/// Escaping only affects rendering. Pings are blocked separately by sending an empty
/// <c>allowed_mentions</c> object with every payload.
/// </remarks>
public static class DiscordMarkdown
{
    /// <summary>Character appended when a text is shortened.</summary>
    public const string Ellipsis = "…";

    // Every character that can start or end Discord markdown, a mention, a custom emoji,
    // a masked link, a heading, a list, a quote or a URL scheme.
    private const string SpecialCharacters = "\\*_~`|<>#-[]()@:.+!";

    /// <summary>Sanitizes then escapes <paramref name="text"/>.</summary>
    /// <param name="text">Untrusted text.</param>
    /// <returns>Text that renders literally in Discord.</returns>
    public static string Escape(string? text)
    {
        var clean = Sanitize(text);
        if (clean.Length == 0)
        {
            return clean;
        }

        var builder = new StringBuilder(clean.Length + 8);
        foreach (var c in clean)
        {
            if (SpecialCharacters.Contains(c, StringComparison.Ordinal))
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Removes control characters and invisible formatting
    /// characters, such as bidi overrides and zero-width spaces, that could be used to
    /// spoof or break the layout. Whitespace runs, line breaks included, collapse to a single space.
    /// </summary>
    /// <param name="text">Untrusted text.</param>
    /// <returns>A single trimmed line.</returns>
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (char.IsControl(c))
            {
                continue;
            }

            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format && c != '‍')
            {
                // Bidi overrides, zero-width spaces, BOM... ZWJ is kept for emoji sequences.
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Shortens <paramref name="text"/> to at most <paramref name="maxLength"/> UTF-16 code units,
    /// appending <see cref="Ellipsis"/>. It never splits a surrogate pair and never leaves a
    /// dangling escape backslash.
    /// </summary>
    /// <param name="text">Text to shorten.</param>
    /// <param name="maxLength">Maximum length, ellipsis included.</param>
    /// <returns>The original text if it fits, otherwise a shortened copy.</returns>
    public static string Truncate(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, Ellipsis.Length);

        if (text.Length <= maxLength)
        {
            return text;
        }

        var cut = maxLength - Ellipsis.Length;
        if (cut > 0 && char.IsHighSurrogate(text[cut - 1]))
        {
            cut--;
        }

        // An odd number of trailing backslashes means the last one escapes nothing anymore.
        var trailing = 0;
        while (trailing < cut && text[cut - 1 - trailing] == '\\')
        {
            trailing++;
        }

        if (trailing % 2 == 1)
        {
            cut--;
        }

        return string.Concat(text.AsSpan(0, cut), Ellipsis);
    }
}
