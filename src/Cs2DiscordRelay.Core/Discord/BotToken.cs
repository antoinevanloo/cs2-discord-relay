using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>
/// A Discord bot token. It grants full control over the bot, so it is never exposed through
/// <see cref="ToString"/>, logs or error messages.
/// </summary>
public sealed partial class BotToken
{
    /// <summary>What is shown instead of the real token.</summary>
    public const string Redacted = "***";

    private BotToken(string value) => Value = value;

    /// <summary>The raw token. Internal so it cannot leak through the public API.</summary>
    internal string Value { get; }

    /// <summary>Value of the <c>Authorization</c> header.</summary>
    internal string AuthorizationHeader => "Bot " + Value;

    /// <summary>Validates a token copied from the Discord Developer Portal.</summary>
    /// <param name="value">The token. A leading <c>Bot </c> is accepted and removed.</param>
    /// <param name="token">The parsed token on success.</param>
    /// <param name="error">Why the token was rejected. Never contains the token itself.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> looks like a bot token.</returns>
    public static bool TryParse(string? value, [NotNullWhen(true)] out BotToken? token, [NotNullWhen(false)] out string? error)
    {
        token = null;
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[4..].Trim();
        }

        if (trimmed.Length == 0)
        {
            error = "BotToken is empty.";
            return false;
        }

        if (!TokenFormat().IsMatch(trimmed))
        {
            error = "BotToken does not look like a Discord bot token (three parts separated by dots). Copy it again from Developer Portal > Bot > Reset Token.";
            return false;
        }

        token = new BotToken(trimmed);
        error = null;
        return true;
    }

    /// <summary>Replaces any occurrence of the token in <paramref name="text"/>.</summary>
    /// <param name="text">Text that may contain the token, such as an exception message.</param>
    /// <returns>The text without the secret.</returns>
    public string Redact(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Replace(Value, Redacted, StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Redacted;

    [GeneratedRegex(@"^[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{4,}\.[A-Za-z0-9_\-]{20,}$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenFormat();
}
