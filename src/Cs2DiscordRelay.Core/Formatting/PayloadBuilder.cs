using System.Text;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Core.Formatting;

/// <summary>
/// Packs a burst of messages into as few webhook requests as Discord's limits allow,
/// preserving their order.
/// </summary>
public static class PayloadBuilder
{
    /// <summary>Groups text lines (≤ 2000 chars) and embeds (≤ 10 and ≤ 6000 chars) per payload.</summary>
    /// <param name="messages">Messages in chronological order.</param>
    /// <param name="options">Options providing the username and avatar overrides.</param>
    /// <returns>Payloads ready to be sent, in order.</returns>
    public static IReadOnlyList<WebhookPayload> Build(IReadOnlyList<FormattedMessage> messages, RelayOptions options)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(options);

        var payloads = new List<WebhookPayload>();
        var text = new StringBuilder();
        var embeds = new List<DiscordEmbed>();
        var embedsLength = 0;

        foreach (var message in messages)
        {
            if (message.Text is { } line)
            {
                FlushEmbeds();
                if (text.Length > 0 && text.Length + 1 + line.Length > DiscordLimits.ContentLength)
                {
                    FlushText();
                }

                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(line);
            }
            else if (message.Embed is { } embed)
            {
                FlushText();
                if (embeds.Count == DiscordLimits.EmbedsPerMessage
                    || (embeds.Count > 0 && embedsLength + embed.TextLength > DiscordLimits.EmbedsTotalLength))
                {
                    FlushEmbeds();
                }

                embeds.Add(embed);
                embedsLength += embed.TextLength;
            }
        }

        FlushText();
        FlushEmbeds();
        return payloads;

        void FlushText()
        {
            if (text.Length == 0)
            {
                return;
            }

            payloads.Add(new WebhookPayload
            {
                Content = text.ToString(),
                Username = options.WebhookUsername,
                AvatarUrl = options.WebhookAvatarUrl,
                Flags = WebhookPayload.SuppressEmbedsFlag,
            });
            text.Clear();
        }

        void FlushEmbeds()
        {
            if (embeds.Count == 0)
            {
                return;
            }

            payloads.Add(new WebhookPayload
            {
                Embeds = [.. embeds],
                Username = options.WebhookUsername,
                AvatarUrl = options.WebhookAvatarUrl,
            });
            embeds.Clear();
            embedsLength = 0;
        }
    }
}
