namespace DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;

/// <summary>
/// Represents a concurrency test scenario as defined in PR-02.7.
/// Each scenario tests thread-safety of a specific library component under concurrent access.
/// </summary>
public interface IConcurrencyScenario
{
    /// <summary>
    /// Name of the scenario (e.g., "Concurrent cache access with same expression").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the scenario with the specified number of concurrent workers.
    /// Throws if the scenario fails (e.g., exceptions, incorrect results, race conditions detected).
    /// </summary>
    Task ExecuteAsync(int concurrentWorkers, CancellationToken cancellationToken = default);
}
