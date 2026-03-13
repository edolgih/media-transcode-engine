using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Cli.Tests.Logging;

/*
Это запись одного лог-события, собранного тестовым logger.
Она фиксирует все полезные поля, чтобы тесты могли делать точные assertions по логированию.
*/
/// <summary>
/// Captures one log entry collected by the CLI test logger.
/// </summary>
internal sealed record LogEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception);

/*
Это тестовый provider логирования для CLI-тестов.
Он накапливает записи в памяти и выдает logger-экземпляры с общим списком событий.
*/
/// <summary>
/// Provides in-memory loggers for CLI tests and keeps all collected log entries.
/// </summary>
internal sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName)
    {
        return new ListLogger(categoryName, _entries);
    }

    public void Dispose()
    {
    }

    /*
    Это внутренний logger, который пишет события в общий in-memory список provider-а.
    Он нужен только тестам и не несет отдельной доменной ответственности.
    */
    /// <summary>
    /// Writes CLI test log events into the shared in-memory entry list.
    /// </summary>
    private sealed class ListLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly List<LogEntry> _entries;

        public ListLogger(string categoryName, List<LogEntry> entries)
        {
            _categoryName = categoryName;
            _entries = entries;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            _entries.Add(new LogEntry(
                Category: _categoryName,
                Level: logLevel,
                Message: formatter(state, exception),
                Properties: properties,
                Exception: exception));
        }
    }

    /*
    Это пустой scope для тестового logger-а.
    Он закрывает контракт ILogger без дополнительного поведения.
    */
    /// <summary>
    /// Represents a no-op logging scope used by the CLI test logger.
    /// </summary>
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
