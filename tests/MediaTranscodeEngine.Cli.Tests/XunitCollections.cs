namespace MediaTranscodeEngine.Cli.Tests;

/*
Это xUnit-коллекция для тестов, которые трогают глобальное состояние консоли.
Она отключает параллелизм, чтобы переназначение stdout/stderr не ломало соседние тесты.
*/
/// <summary>
/// Groups console-sensitive tests and disables parallel execution for them.
/// </summary>
[CollectionDefinition("Console", DisableParallelization = true)]
public sealed class ConsoleCollectionDefinition
{
}
