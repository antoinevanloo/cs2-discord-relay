using System.Globalization;

namespace Cs2DiscordRelay.Core.Options;

/// <summary>Parses <c>#RRGGBB</c> colors into the integer form Discord expects.</summary>
public static class HexColor
{
    /// <summary>Parses <c>#RRGGBB</c> or <c>RRGGBB</c> (case-insensitive).</summary>
    /// <param name="value">The hex string.</param>
    /// <param name="color">The 24-bit color on success.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> is a valid color.</returns>
    public static bool TryParse(string? value, out int color)
    {
        color = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var span = value.AsSpan().Trim();
        if (span.StartsWith('#'))
        {
            span = span[1..];
        }

        return span.Length == 6
            && int.TryParse(span, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out color);
    }
}
