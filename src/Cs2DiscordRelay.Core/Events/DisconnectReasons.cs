namespace Cs2DiscordRelay.Core.Events;

/// <summary>
/// Human readable labels for the most common values of the engine's
/// <c>ENetworkDisconnectionReason</c> protobuf enum (sent in <c>player_disconnect.reason</c>).
/// </summary>
public static class DisconnectReasons
{
    private static readonly Dictionary<int, string> Labels = new()
    {
        [1] = "Server shutting down",
        [2] = "Disconnected",
        [3] = "Disconnected by server",
        [4] = "Lost connection",
        [6] = "Banned by Steam",
        [13] = "VAC banned",
        [14] = "Logged in elsewhere",
        [16] = "Steam connection lost",
        [29] = "Timed out",
        [30] = "Disconnected",
        [39] = "Kicked",
        [40] = "Banned",
        [41] = "Kicked and banned",
        [49] = "Wrong server password",
        [54] = "Server shutting down",
        [55] = "Server shutting down",
        [56] = "Game ended",
        [59] = "Game closed",
        [69] = "Server shutting down",
        [79] = "Timed out",
        [80] = "Timed out while connecting",
    };

    /// <summary>Returns a label for <paramref name="code"/>, or <see langword="null"/> when unknown.</summary>
    /// <param name="code">Raw reason code.</param>
    /// <returns>The label, if known.</returns>
    public static string? Describe(int code) => Labels.GetValueOrDefault(code);
}
