using System.Reflection;
using System.Runtime.CompilerServices;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;

namespace Cs2DiscordRelay.Tests;

/// <summary>
/// Guards the conventions CounterStrikeSharp relies on at load time. The plugin cannot be constructed
/// outside a server (<c>BasePlugin</c>'s constructor calls native code), so these checks use reflection.
/// </summary>
public sealed class PluginMetadataTests
{
    private static readonly Type PluginType = typeof(Cs2DiscordRelayPlugin);

    [Fact]
    public void Module_name_matches_the_dll_and_plugin_folder()
    {
        // css_plugins reload looks plugins up by ModuleName; the folder must be named after the DLL.
        var plugin = (Cs2DiscordRelayPlugin)RuntimeHelpers.GetUninitializedObject(PluginType);

        Assert.Equal(PluginType.Assembly.GetName().Name, plugin.ModuleName);
    }

    [Fact]
    public void Requires_the_api_version_that_introduced_OnPlayerChat() =>
        Assert.Equal(333, PluginType.GetCustomAttribute<MinimumApiVersion>()?.Version);

    [Fact]
    public void Admin_commands_are_discoverable_and_restricted()
    {
        // CounterStrikeSharp only scans public methods (BasePlugin.RegisterConsoleCommandAttributeHandlers).
        var commands = PluginType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(m => m.GetCustomAttributes<ConsoleCommandAttribute>().Select(a => (Method: m, a.Command)))
            .ToList();

        Assert.Equal(["css_discordrelay_status", "css_discordrelay_test"], commands.Select(c => c.Command).Order());
        Assert.All(commands, c =>
        {
            Assert.True(c.Method.IsPublic, $"{c.Command} must be public to be registered.");
            Assert.Contains("@css/root", c.Method.GetCustomAttribute<RequiresPermissions>()!.Permissions);
        });
    }
}
