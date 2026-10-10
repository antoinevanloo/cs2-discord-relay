namespace Cs2DiscordRelay.Core.Discord;

/// <summary>Gateway opcodes used by the bridge (https://discord.com/developers/docs/topics/opcodes-and-status-codes).</summary>
internal static class GatewayOpcodes
{
    public const int Dispatch = 0;
    public const int Heartbeat = 1;
    public const int Identify = 2;
    public const int Resume = 6;
    public const int Reconnect = 7;
    public const int InvalidSession = 9;
    public const int Hello = 10;
    public const int HeartbeatAck = 11;
}
