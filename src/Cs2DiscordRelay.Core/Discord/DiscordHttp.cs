using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace Cs2DiscordRelay.Core.Discord;

/// <summary>Creates the single <see cref="HttpClient"/> the relay uses for its whole lifetime.</summary>
public static class DiscordHttp
{
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
        var version = typeof(DiscordHttp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("cs2-discord-relay", version.Split('+')[0]));
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/antoinevanloo/cs2-discord-relay)"));
        return client;
    }
}
