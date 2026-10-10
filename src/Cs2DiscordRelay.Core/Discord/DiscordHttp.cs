using System.Net;
using System.Reflection;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>Creates the single <see cref="HttpClient"/> the relay uses for its whole lifetime.</summary>
public static class DiscordHttp
{
    /// <summary>Project URL, sent in the User-Agent as Discord requires.</summary>
    public const string ProjectUrl = "https://github.com/antoinevanloo/cs2-discord-relay";

    /// <summary>Per-request timeout.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Creates a client with connection pooling and periodic DNS refresh.
    /// Dispose it when the plugin unloads.
    /// </summary>
    /// <returns>The client.</returns>
    public static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = DecompressionMethods.All,
        };

        var client = new HttpClient(handler, disposeHandler: true) { Timeout = RequestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    // Version must stay above UserAgent: static initializers run in textual order.
    /// <summary>Plugin version, without build metadata.</summary>
    public static string Version { get; } =
        (typeof(DiscordHttp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0").Split('+')[0];

    /// <summary>
    /// <c>DiscordBot ($url, $versionNumber)</c>, the format required by the Discord HTTP API
    /// (https://discord.com/developers/docs/reference#user-agent).
    /// </summary>
    public static string UserAgent { get; } = $"DiscordBot ({ProjectUrl}, {Version})";
}
