using Microsoft.Extensions.Logging;

namespace Cs2DiscordRelay;

internal static partial class PluginLog
{
    [LoggerMessage(100, LogLevel.Warning, "Config: {Warning}")]
    public static partial void ConfigWarning(ILogger logger, string warning);

    [LoggerMessage(101, LogLevel.Warning, "{Error} The Discord relay is disabled. Set \"WebhookUrl\" in {ConfigPath}, then run: css_plugins reload Cs2DiscordRelay")]
    public static partial void RelayDisabled(ILogger logger, string error, string configPath);

    [LoggerMessage(102, LogLevel.Information, "Discord relay started ({Format} messages).")]
    public static partial void RelayStarted(ILogger logger, string format);

    [LoggerMessage(103, LogLevel.Error, "Could not relay {EventType}: {Error}")]
    public static partial void PublishFailed(ILogger logger, string eventType, string error);

    [LoggerMessage(104, LogLevel.Warning, "{Error} The Discord to game relay is disabled. Set \"DiscordToGame.BotToken\" in {ConfigPath}, then run: css_plugins reload Cs2DiscordRelay")]
    public static partial void BridgeDisabled(ILogger logger, string error, string configPath);

    [LoggerMessage(105, LogLevel.Information, "Your configuration file is version {Version}. Version {CurrentVersion} adds the \"DiscordToGame\" section (Discord messages in the game chat), see the README.")]
    public static partial void ConfigOutdated(ILogger logger, int version, int currentVersion);

    [LoggerMessage(106, LogLevel.Error, "Could not show Discord messages in game: {Error}")]
    public static partial void BridgeDrainFailed(ILogger logger, string error);
}
