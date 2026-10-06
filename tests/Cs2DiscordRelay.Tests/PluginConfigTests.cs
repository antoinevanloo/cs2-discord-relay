using System.Text.Json;
using Cs2DiscordRelay.Core.Options;

namespace Cs2DiscordRelay.Tests;

public sealed class PluginConfigTests
{
    // Same settings as CounterStrikeSharp's ConfigManager (Modules/Config/ConfigManager.cs).
    private static readonly JsonSerializerOptions CssOptions = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [Fact]
    public void Generated_defaults_have_the_documented_shape()
    {
        var json = JsonSerializer.Serialize(new PluginConfig(), CssOptions);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(string.Empty, root.GetProperty("WebhookUrl").GetString());
        Assert.Equal("Embed", root.GetProperty("MessageFormat").GetString());
        Assert.True(root.GetProperty("Events").GetProperty("TeamChat").GetBoolean());
        Assert.Equal("#5865F2", root.GetProperty("Colors").GetProperty("Chat").GetString());
        Assert.Equal(1000, root.GetProperty("BatchWindowMs").GetInt32());
        Assert.Equal(1, root.GetProperty("ConfigVersion").GetInt32());
    }

    [Fact]
    public void Commented_config_from_the_readme_is_read()
    {
        const string json = """
            {
              // Secret: never share it.
              "WebhookUrl": "https://discord.com/api/webhooks/1/x",
              "MessageFormat": "Text",
              "Events": { "Chat": true, "TeamChat": false, "PlayerConnect": true, "PlayerDisconnect": false, "MapChange": true },
              "CommandPrefixes": [ "." ],
              "BatchWindowMs": 0,
              "ConfigVersion": 1
            }
            """;

        var config = JsonSerializer.Deserialize<PluginConfig>(json, CssOptions)!;

        Assert.Equal(MessageFormat.Text, config.MessageFormat);
        Assert.False(config.Events.TeamChat);
        Assert.False(config.Events.PlayerDisconnect);
        Assert.Equal(["."], config.CommandPrefixes);
        Assert.Equal(0, config.BatchWindowMs);
        Assert.True(config.IgnoreBots); // missing keys keep their defaults
    }

    [Fact]
    public void Numeric_message_format_is_still_accepted()
    {
        // JsonStringEnumConverter also accepts the numeric form, which keeps hand-edited configs working.
        var config = JsonSerializer.Deserialize<PluginConfig>("""{ "MessageFormat": 1 }""", CssOptions)!;

        Assert.Equal(MessageFormat.Text, config.MessageFormat);
    }

    [Fact]
    public void Maps_to_normalized_relay_options()
    {
        var warnings = new List<string>();
        var config = new PluginConfig
        {
            MessageFormat = MessageFormat.Text,
            BatchWindowMs = 250,
            MaxQueueSize = 5,
            WebhookUsername = " Server ",
            Colors = new EventColors { Chat = "nope" },
        };

        var options = config.ToRelayOptions(warnings);

        Assert.Equal(MessageFormat.Text, options.Format);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.BatchWindow);
        Assert.Equal(RelayOptions.MinQueueSize, options.MaxQueueSize);
        Assert.Equal("Server", options.WebhookUsername);
        Assert.Equal(EventColors.DefaultChat, options.Colors.Chat);
        Assert.Equal(2, warnings.Count);
    }
}
