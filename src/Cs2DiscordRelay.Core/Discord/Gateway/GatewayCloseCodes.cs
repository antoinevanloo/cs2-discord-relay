namespace Cs2DiscordRelay.Core.Discord.Gateway;

/// <summary>What to do after the gateway closed the connection.</summary>
internal enum CloseAction
{
    /// <summary>Reconnect and resume the session.</summary>
    Resume,

    /// <summary>Reconnect with a fresh session (new Identify).</summary>
    Reidentify,

    /// <summary>Do not reconnect: the configuration must be fixed first.</summary>
    Fatal,
}

/// <summary>Gateway close codes (https://discord.com/developers/docs/topics/opcodes-and-status-codes#gateway-gateway-close-event-codes).</summary>
internal static class GatewayCloseCodes
{
    /// <summary>Client close code that invalidates the session (clean shutdown).</summary>
    public const int NormalClosure = 1000;

    /// <summary>Client close code that keeps the session resumable.</summary>
    public const int Resumable = 4000;

    public const int AuthenticationFailed = 4004;
    public const int InvalidSequence = 4007;
    public const int SessionTimedOut = 4009;
    public const int InvalidShard = 4010;
    public const int ShardingRequired = 4011;
    public const int InvalidApiVersion = 4012;
    public const int InvalidIntents = 4013;
    public const int DisallowedIntents = 4014;

    /// <summary>Classifies a close code; <see langword="null"/> means the connection dropped without one.</summary>
    public static CloseAction Classify(int? code) => code switch
    {
        AuthenticationFailed or InvalidShard or ShardingRequired or InvalidApiVersion or InvalidIntents or DisallowedIntents => CloseAction.Fatal,
        InvalidSequence or SessionTimedOut => CloseAction.Reidentify,
        _ => CloseAction.Resume,
    };

    /// <summary>Explains a fatal close code and how to fix it.</summary>
    public static string DescribeFatal(int code) => code switch
    {
        AuthenticationFailed =>
            "Discord rejected the bot token (close code 4004). Reset the token in Developer Portal > Bot and update DiscordToGame.BotToken.",
        DisallowedIntents =>
            "Message Content Intent is not enabled for this bot (close code 4014). Enable it in Developer Portal > Bot > Privileged Gateway Intents.",
        InvalidIntents => "Discord rejected the requested intents (close code 4013).",
        _ => $"Discord closed the gateway with code {code}, which cannot be recovered by reconnecting.",
    };
}
