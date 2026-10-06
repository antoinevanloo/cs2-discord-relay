namespace Cs2DiscordRelay.Core.Events;

/// <summary>
/// A game-agnostic snapshot of something that happened on the server.
/// Instances are immutable and safe to hand over to a background thread.
/// </summary>
/// <param name="Timestamp">When the event happened (UTC).</param>
public abstract record RelayEvent(DateTimeOffset Timestamp);

/// <summary>A chat message sent by a player.</summary>
/// <param name="Timestamp">When the message was sent.</param>
/// <param name="Player">Who sent it.</param>
/// <param name="Message">Raw message text, as typed by the player.</param>
/// <param name="TeamOnly"><see langword="true"/> for team chat (<c>say_team</c>).</param>
public sealed record ChatMessageEvent(DateTimeOffset Timestamp, PlayerInfo Player, string Message, bool TeamOnly)
    : RelayEvent(Timestamp);

/// <summary>A player finished connecting to the server.</summary>
/// <param name="Timestamp">When the player connected.</param>
/// <param name="Player">The player.</param>
public sealed record PlayerConnectedEvent(DateTimeOffset Timestamp, PlayerInfo Player) : RelayEvent(Timestamp);

/// <summary>A player left the server.</summary>
/// <param name="Timestamp">When the player disconnected.</param>
/// <param name="Player">The player.</param>
/// <param name="Reason">Human readable disconnect reason, if known.</param>
public sealed record PlayerDisconnectedEvent(DateTimeOffset Timestamp, PlayerInfo Player, string? Reason)
    : RelayEvent(Timestamp);

/// <summary>A new map was loaded.</summary>
/// <param name="Timestamp">When the map started.</param>
/// <param name="MapName">Technical map name, e.g. <c>de_dust2</c>.</param>
public sealed record MapChangedEvent(DateTimeOffset Timestamp, string MapName) : RelayEvent(Timestamp);

/// <summary>A test message requested by an admin to check the webhook. Never filtered.</summary>
/// <param name="Timestamp">When the test was requested.</param>
/// <param name="RequestedBy">Who requested it (a player name, or <c>Console</c>).</param>
public sealed record RelayTestEvent(DateTimeOffset Timestamp, string RequestedBy) : RelayEvent(Timestamp);
