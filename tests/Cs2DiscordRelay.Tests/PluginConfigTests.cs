using System.Text.Json;
using Cs2DiscordRelay.Core.Inbound;
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
        Assert.Single(json.Split('\n'), l => l.Contains("\"ConfigVersion\"", StringComparison.Ordinal)); // the override does not duplicate it

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(string.Empty, root.GetProperty("WebhookUrl").GetString());
        Assert.Equal("Embed", root.GetProperty("MessageFormat").GetString());
        Assert.True(root.GetProperty("Events").GetProperty("TeamChat").GetBoolean());
        Assert.Equal("#5865F2", root.GetProperty("Colors").GetProperty("Chat").GetString());
        Assert.Equal(1000, root.GetProperty("BatchWindowMs").GetInt32());
        Assert.Equal(2, root.GetProperty("ConfigVersion").GetInt32());

        var discord = root.GetProperty("DiscordToGame");
        Assert.False(discord.GetProperty("Enabled").GetBoolean());
        Assert.Equal(string.Empty, discord.GetProperty("BotToken").GetString());
        Assert.Equal(string.Empty, discord.GetProperty("ChannelId").GetString());
        Assert.Equal(0, discord.GetProperty("AllowedRoleIds").GetArrayLength());
        Assert.Equal(InboundOptions.DefaultGameChatFormat, discord.GetProperty("GameChatFormat").GetString());
        Assert.Equal(200, discord.GetProperty("MaxMessageLength").GetInt32());
        Assert.Equal(5, discord.GetProperty("MaxMessagesPer10Seconds").GetInt32());
        Assert.Equal(2, discord.GetProperty("UserCooldownSeconds").GetInt32());
    }

    [Fact]
    public void Version_1_files_load_with_the_discord_relay_disabled()
    {
        var config = JsonSerializer.Deserialize<PluginConfig>("""{ "WebhookUrl": "", "ConfigVersion": 1 }""", CssOptions)!;

        Assert.Equal(1, config.Version);
        Assert.False(config.DiscordToGame.Enabled);
    }

    [Fact]
    public void Maps_discord_to_game_settings()
    {
        var warnings = new List<string>();
        var section = new DiscordToGameConfig
        {
            ChannelId = " 555 ",
            AllowedRoleIds = ["100", "@admins", "200"],
            MaxMessageLength = 1000,
            UserCooldownSeconds = 5,
        };

        var options = section.ToInboundOptions(warnings);

        Assert.Equal(555UL, options.ChannelId);
        Assert.Equal([100UL, 200UL], options.AllowedRoleIds);
        Assert.Equal(InboundOptions.MaxMessageLengthLimit, options.MaxMessageLength);
        Assert.Equal(TimeSpan.FromSeconds(5), options.UserCooldown);
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void Invalid_channel_id_falls_back_to_the_webhook_channel()
    {
        var warnings = new List<string>();

        var options = new DiscordToGameConfig { ChannelId = "#general" }.ToInboundOptions(warnings);

        Assert.Null(options.ChannelId);
        Assert.Contains("Copy Channel ID", Assert.Single(warnings), StringComparison.Ordinal);
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
              "DiscordToGame": { "Enabled": true, "BotToken": "x", "AllowedRoleIds": [ "1" ] },
              "ConfigVersion": 2
            }
            """;

        var config = JsonSerializer.Deserialize<PluginConfig>(json, CssOptions)!;

        Assert.Equal(MessageFormat.Text, config.MessageFormat);
        Assert.False(config.Events.TeamChat);
        Assert.False(config.Events.PlayerDisconnect);
        Assert.Equal(["."], config.CommandPrefixes);
        Assert.Equal(0, config.BatchWindowMs);
        Assert.True(config.IgnoreBots); // missing keys keep their defaults
        Assert.True(config.DiscordToGame.Enabled);
        Assert.Equal(["1"], config.DiscordToGame.AllowedRoleIds);
        Assert.Equal(5, config.DiscordToGame.MaxMessagesPer10Seconds);
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
