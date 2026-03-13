using Microsoft.Extensions.Logging;

namespace MediaTranscodeEngine.Runtime.Tests.Logging;

/*
Это запись одного лог-события для runtime-тестов.
Она хранит уровень, текст, свойства и исключение для точных assertions.
*/
/// <summary>
/// Captures one log entry collected by the runtime test logger.
/// </summary>
internal sealed record LogEntry(
    LogLevel Level,
    string Message,
    IReadOnlyDictionary<string, object?> Properties,
    Exception? Exception);

/*
Это in-memory logger для runtime-тестов.
Он позволяет проверять диагностический вывод runtime-компонентов без внешней инфраструктуры.
*/
/// <summary>
/// Collects runtime log events in memory for test assertions.
/// </summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

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
            Level: logLevel,
            Message: formatter(state, exception),
            Properties: properties,
            Exception: exception));
    }

    /*
    Это пустой scope для тестового logger-а runtime.
    Он нужен только для реализации интерфейса ILogger.
    */
    /// <summary>
    /// Represents a no-op logging scope used by the runtime test logger.
    /// </summary>
    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
