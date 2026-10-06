using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Cs2DiscordRelay.Core;
using Cs2DiscordRelay.Core.Discord;
using Cs2DiscordRelay.Core.Events;

namespace Cs2DiscordRelay;

/// <summary>
/// CounterStrikeSharp entry point. This layer only copies game data into plain events;
/// filtering, formatting and sending live in <c>Cs2DiscordRelay.Core</c>.
/// </summary>
[MinimumApiVersion(333)] // Listeners.OnPlayerChat
public sealed class Cs2DiscordRelayPlugin : BasePlugin, IPluginConfig<PluginConfig>
{
    private const string ConfigPath = "addons/counterstrikesharp/configs/plugins/Cs2DiscordRelay/Cs2DiscordRelay.json";
    private const string ReplyPrefix = "[Cs2DiscordRelay] ";

    // The server console and RCON always pass this check (no calling player).
    private const string AdminPermission = "@css/root";

    // Survives relay restarts (config reloads), so a reload never re-announces everyone.
    private readonly PresenceTracker _presence = new();
    private RelayService? _relay;
    private string? _disabledReason;

    /// <summary>
    /// Same identifier as the folder, DLL and config file: <c>css_plugins reload</c> looks plugins up by
    /// <c>ModuleName</c>, so <c>css_plugins reload Cs2DiscordRelay</c> works as documented.
    /// </summary>
    public override string ModuleName => "Cs2DiscordRelay";

    /// <inheritdoc />
    public override string ModuleVersion { get; } =
        typeof(Cs2DiscordRelayPlugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "1.0.0";

    /// <inheritdoc />
    public override string ModuleAuthor => "LinKeD";

    /// <inheritdoc />
    public override string ModuleDescription => "Relays chat, connections and map changes to a Discord webhook.";

    /// <inheritdoc />
    public PluginConfig Config { get; set; } = new();

    /// <inheritdoc />
    public void OnConfigParsed(PluginConfig config) => Config = config;

    /// <inheritdoc />
    public override void Load(bool hotReload)
    {
        StartRelay();

        RegisterListener<Listeners.OnPlayerChat>(OnPlayerChat);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(_presence.MapEnding);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
    }

    /// <inheritdoc />
    public override void Unload(bool hotReload)
    {
        var relay = _relay;
        _relay = null;
        if (relay is null)
        {
            return;
        }

        // Flush in the background: never block the game thread on network I/O.
        _ = Task.Run(async () => await relay.DisposeAsync().ConfigureAwait(false));
    }

    private void StartRelay()
    {
        var warnings = new List<string>();
        var options = Config.ToRelayOptions(warnings);
        foreach (var warning in warnings)
        {
            PluginLog.ConfigWarning(Logger, warning);
        }

        if (!WebhookUrl.TryParse(Config.WebhookUrl, out var url, out var error))
        {
            _disabledReason = error;
            PluginLog.RelayDisabled(Logger, error, ConfigPath);
            return;
        }

        _relay = new RelayService(url, options, Logger);
        PluginLog.RelayStarted(Logger, options.Format.ToString());
    }

    /// <summary>Sends a test message, so admins can check the webhook without waiting for players.</summary>
    /// <param name="caller">The admin who ran the command, or <see langword="null"/> for the server console.</param>
    /// <param name="command">The command.</param>
    [ConsoleCommand("css_discordrelay_test", "Sends a test message to the Discord webhook.")]
    [RequiresPermissions(AdminPermission)]
    public void OnTestCommand(CCSPlayerController? caller, CommandInfo command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_relay is not { IsEnabled: true } relay)
        {
            command.ReplyToCommand(ReplyPrefix + DescribeStatus());
            return;
        }

        var requestedBy = caller is { IsValid: true } ? caller.PlayerName : "Console";
        command.ReplyToCommand(ReplyPrefix + (relay.Publish(new RelayTestEvent(DateTimeOffset.UtcNow, requestedBy))
            ? "Test message queued. It should appear in Discord within a few seconds; delivery errors are written to the server log."
            : "The relay is shutting down: test message not sent."));
    }

    /// <summary>Prints the state of the relay: running or not, format, relayed events, pending messages.</summary>
    /// <param name="caller">The admin who ran the command, or <see langword="null"/> for the server console.</param>
    /// <param name="command">The command.</param>
    [ConsoleCommand("css_discordrelay_status", "Shows the state of the Discord relay.")]
    [RequiresPermissions(AdminPermission)]
    public void OnStatusCommand(CCSPlayerController? caller, CommandInfo command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.ReplyToCommand($"{ReplyPrefix}v{ModuleVersion}. {DescribeStatus()}");
    }

    private string DescribeStatus() =>
        _relay?.DescribeStatus()
        ?? $"Disabled: {_disabledReason} Set \"WebhookUrl\" in {ConfigPath}, then run: css_plugins reload Cs2DiscordRelay";

    private void OnPlayerChat(CCSPlayerController player, string message, bool teamChat)
    {
        if (_relay is null || player is null || !player.IsValid || player.IsHLTV)
        {
            return;
        }

        Publish(new ChatMessageEvent(DateTimeOffset.UtcNow, PlayerSnapshot.From(player), message, teamChat));
    }

    private void OnMapStart(string mapName)
    {
        _presence.MapStarted();
        Publish(new MapChangedEvent(DateTimeOffset.UtcNow, mapName));
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (_relay is not null && player is { IsValid: true, IsHLTV: false })
        {
            var now = DateTimeOffset.UtcNow;
            var snapshot = PlayerSnapshot.From(player);
            if (_presence.ShouldAnnounceConnect(snapshot, now))
            {
                Publish(new PlayerConnectedEvent(now, snapshot));
            }
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        // Players who never finished connecting were never announced: stay silent for them too.
        if (_relay is null || !@event.EverFullyConnected)
        {
            return HookResult.Continue;
        }

        // The controller may already be invalid here: rely on the event's own fields first.
        var controller = @event.Userid is { IsValid: true } valid ? valid : null;
        if (controller?.IsHLTV == true)
        {
            return HookResult.Continue;
        }

        var isBot = controller?.IsBot ?? string.Equals(@event.Networkid, "BOT", StringComparison.OrdinalIgnoreCase);
        var player = new PlayerInfo(
            @event.Name ?? controller?.PlayerName ?? string.Empty,
            isBot ? 0 : @event.Xuid,
            controller is null ? PlayerTeam.None : (PlayerTeam)(int)controller.Team,
            isBot);

        var now = DateTimeOffset.UtcNow;
        if (_presence.ShouldAnnounceDisconnect(player, @event.Reason, now))
        {
            Publish(new PlayerDisconnectedEvent(now, player, DisconnectReasons.Describe(@event.Reason)));
        }

        return HookResult.Continue;
    }

    private void Publish(RelayEvent relayEvent)
    {
        try
        {
            _relay?.Publish(relayEvent);
        }
#pragma warning disable CA1031 // Never let the relay break a game callback.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            PluginLog.PublishFailed(Logger, relayEvent.GetType().Name, ex.Message);
        }
    }
}
