using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Cs2DiscordRelay.Core.Tests.Fakes;

internal sealed class ListLogger : ILogger
{
    public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

    public IEnumerable<string> Warnings =>
        Entries.Where(e => e.Level >= LogLevel.Warning).Select(e => e.Message);

    public string AllText => string.Join('\n', Entries.Select(e => e.Message));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Enqueue((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " " + exception)));
}
