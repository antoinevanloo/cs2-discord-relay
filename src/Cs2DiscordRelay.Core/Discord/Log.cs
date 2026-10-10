using Microsoft.Extensions.Logging;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>High-performance log messages. None of them ever receives the webhook URL.</summary>
internal static partial class Log
{
    [LoggerMessage(1, LogLevel.Warning, "Discord rejected the webhook (HTTP {StatusCode}): it was deleted or the URL is wrong. Relay disabled until the plugin is reloaded.")]
    public static partial void WebhookRejected(ILogger logger, int statusCode);

    [LoggerMessage(2, LogLevel.Warning, "Discord refused a message (HTTP {StatusCode}: {DiscordMessage}). It was dropped.")]
    public static partial void PayloadRejected(ILogger logger, int statusCode, string discordMessage);

    [LoggerMessage(3, LogLevel.Debug, "Rate limited by Discord, retrying in {RetryAfterMs} ms.")]
    public static partial void RateLimited(ILogger logger, double retryAfterMs);

    [LoggerMessage(4, LogLevel.Warning, "Still rate limited by Discord after {Attempts} attempts, a message was dropped.")]
    public static partial void RateLimitGaveUp(ILogger logger, int attempts);

    [LoggerMessage(5, LogLevel.Debug, "Transient error sending to Discord ({Error}), attempt {Attempt}/{MaxAttempts}, retrying in {DelayMs} ms.")]
    public static partial void TransientFailure(ILogger logger, string error, int attempt, int maxAttempts, double delayMs);

    [LoggerMessage(6, LogLevel.Warning, "Could not reach Discord after {Attempts} attempts ({Error}), a message was dropped.")]
    public static partial void TransientGaveUp(ILogger logger, int attempts, string error);

    [LoggerMessage(7, LogLevel.Warning, "Relay queue is full: {Count} message(s) were dropped. Discord may be slow or rate limiting this webhook.")]
    public static partial void MessagesDropped(ILogger logger, long count);

    [LoggerMessage(8, LogLevel.Error, "Unexpected error in the Discord relay loop ({ErrorType}: {Error}). Relay stopped.")]
    public static partial void UnexpectedError(ILogger logger, string errorType, string error);

    [LoggerMessage(9, LogLevel.Warning, "Shutting down with {Count} message(s) still queued, they were not sent.")]
    public static partial void UnsentOnShutdown(ILogger logger, int count);

    [LoggerMessage(20, LogLevel.Information, "Discord gateway connected as {BotName}.")]
    public static partial void GatewayConnected(ILogger logger, string botName);

    [LoggerMessage(21, LogLevel.Debug, "Discord gateway session resumed.")]
    public static partial void GatewayResumed(ILogger logger);

    [LoggerMessage(22, LogLevel.Information, "Discord gateway disconnected ({Reason}), reconnecting in {DelayMs} ms.")]
    public static partial void GatewayReconnecting(ILogger logger, string reason, double delayMs);

    [LoggerMessage(23, LogLevel.Error, "Discord gateway stopped: {Reason} Discord to game relay disabled until the plugin is reloaded.")]
    public static partial void GatewayStopped(ILogger logger, string reason);

    [LoggerMessage(24, LogLevel.Error, "Could not handle a Discord message ({ErrorType}: {Error}).")]
    public static partial void GatewayHandlerFailed(ILogger logger, string errorType, string error);

    [LoggerMessage(25, LogLevel.Information, "Discord to game relay: reading #{ChannelName} as {BotName}.")]
    public static partial void BridgeReading(ILogger logger, string channelName, string botName);

    [LoggerMessage(26, LogLevel.Warning, "Discord to game relay: {Count} message(s) were not shown in game (rate limit: MaxMessagesPer10Seconds / UserCooldownSeconds).")]
    public static partial void BridgeRateLimited(ILogger logger, long count);

    [LoggerMessage(27, LogLevel.Warning, "Discord to game relay: cannot reach Discord ({Error}), retrying in {DelayMs} ms.")]
    public static partial void BridgeRetrying(ILogger logger, string error, double delayMs);
}
