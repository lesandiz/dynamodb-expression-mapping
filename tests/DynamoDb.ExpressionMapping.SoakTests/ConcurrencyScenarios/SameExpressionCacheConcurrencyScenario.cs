using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;

/// <summary>
/// PR-02.7 Scenario 1: Concurrent cache access with same expression.
/// Multiple threads build the same projection simultaneously.
/// Assert: all get identical results, no exceptions.
/// </summary>
public class SameExpressionCacheConcurrencyScenario : IConcurrencyScenario
{
    private readonly SharedDependencies _sharedDependencies;

    public SameExpressionCacheConcurrencyScenario(SharedDependencies sharedDependencies)
    {
        _sharedDependencies = sharedDependencies ?? throw new ArgumentNullException(nameof(sharedDependencies));
    }

    public string Name => "Concurrent cache access with same expression";

    public async Task ExecuteAsync(int concurrentWorkers, CancellationToken cancellationToken = default)
    {
        var results = new string?[concurrentWorkers];
        var exceptions = new Exception?[concurrentWorkers];
        var tasks = new Task[concurrentWorkers];

        // All workers build the exact same projection
        for (int i = 0; i < concurrentWorkers; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    var builder = new ProjectionBuilder<TestEntity>(
                        _sharedDependencies.ResolverFactory,
                        cache: _sharedDependencies.ExpressionCache);

                    // Build the same projection expression
                    var result = builder.BuildProjection(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Status
                    });

                    results[index] = result.ProjectionExpression;
                }
                catch (Exception ex)
                {
                    exceptions[index] = ex;
                }
            }, cancellationToken);
        }

        await Task.WhenAll(tasks);

        // Assert: no exceptions occurred
        var failedWorkers = exceptions.Select((ex, idx) => (ex, idx)).Where(x => x.ex != null).ToList();
        if (failedWorkers.Any())
        {
            var errorMessages = string.Join(", ", failedWorkers.Select(x => $"Worker {x.idx}: {x.ex!.Message}"));
            throw new InvalidOperationException($"Scenario '{Name}' failed with exceptions: {errorMessages}");
        }

        // Assert: all results are identical
        var firstResult = results[0];
        if (firstResult == null)
        {
            throw new InvalidOperationException($"Scenario '{Name}' failed: first result was null");
        }

        for (int i = 1; i < results.Length; i++)
        {
            if (results[i] != firstResult)
            {
                throw new InvalidOperationException(
                    $"Scenario '{Name}' failed: result mismatch at worker {i}. " +
                    $"Expected: {firstResult}, Got: {results[i]}");
            }
        }
    }

    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
