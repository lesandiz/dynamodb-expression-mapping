using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;

/// <summary>
/// PR-02.7 Scenario 2: Concurrent cache access with distinct expressions.
/// Multiple threads build unique expressions simultaneously.
/// Assert: cache grows correctly, no entries lost or corrupted.
/// </summary>
public class DistinctExpressionCacheConcurrencyScenario : IConcurrencyScenario
{
    private readonly SharedDependencies _sharedDependencies;

    public DistinctExpressionCacheConcurrencyScenario(SharedDependencies sharedDependencies)
    {
        _sharedDependencies = sharedDependencies ?? throw new ArgumentNullException(nameof(sharedDependencies));
    }

    public string Name => "Concurrent cache access with distinct expressions";

    public async Task ExecuteAsync(int concurrentWorkers, CancellationToken cancellationToken = default)
    {
        var initialCacheSize = _sharedDependencies.ExpressionCache.GetStatistics().TotalEntries;
        var results = new (string expression, int workerIndex)[concurrentWorkers];
        var exceptions = new Exception?[concurrentWorkers];
        var tasks = new Task[concurrentWorkers];

        // Each worker builds a unique projection
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

                    // Build a unique projection for each worker
                    var result = index switch
                    {
                        0 => builder.BuildProjection(e => new { e.Id }),
                        1 => builder.BuildProjection(e => new { e.Name }),
                        2 => builder.BuildProjection(e => new { e.Status }),
                        3 => builder.BuildProjection(e => new { e.Id, e.Name }),
                        4 => builder.BuildProjection(e => new { e.Name, e.Status }),
                        5 => builder.BuildProjection(e => new { e.Id, e.Status }),
                        6 => builder.BuildProjection(e => new { e.Id, e.Name, e.Status }),
                        7 => builder.BuildProjection(e => new { e.Value }),
                        _ => builder.BuildProjection(e => new { e.Id, e.Value }) // fallback for more workers
                    };

                    results[index] = (result.ProjectionExpression, index);
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

        // Assert: all workers got a result
        if (results.Any(r => r.expression == null))
        {
            var nullIndices = results.Select((r, idx) => (r, idx)).Where(x => x.r.expression == null).Select(x => x.idx);
            throw new InvalidOperationException(
                $"Scenario '{Name}' failed: null results at workers {string.Join(", ", nullIndices)}");
        }

        // Assert: cache grew by at least some entries (accounting for duplicates from fallback)
        var finalCacheSize = _sharedDependencies.ExpressionCache.GetStatistics().TotalEntries;
        var expectedMinGrowth = Math.Min(7, concurrentWorkers); // 7 unique expressions defined above

        if (finalCacheSize < initialCacheSize + expectedMinGrowth)
        {
            throw new InvalidOperationException(
                $"Scenario '{Name}' failed: cache did not grow as expected. " +
                $"Initial: {initialCacheSize}, Final: {finalCacheSize}, Expected min growth: {expectedMinGrowth}");
        }

        // Assert: verify we can retrieve all unique expressions from cache (no corruption)
        for (int i = 0; i < Math.Min(concurrentWorkers, 7); i++)
        {
            var builder = new ProjectionBuilder<TestEntity>(
                _sharedDependencies.ResolverFactory,
                cache: _sharedDependencies.ExpressionCache);

            string cachedResult = i switch
            {
                0 => builder.BuildProjection(e => new { e.Id }).ProjectionExpression,
                1 => builder.BuildProjection(e => new { e.Name }).ProjectionExpression,
                2 => builder.BuildProjection(e => new { e.Status }).ProjectionExpression,
                3 => builder.BuildProjection(e => new { e.Id, e.Name }).ProjectionExpression,
                4 => builder.BuildProjection(e => new { e.Name, e.Status }).ProjectionExpression,
                5 => builder.BuildProjection(e => new { e.Id, e.Status }).ProjectionExpression,
                6 => builder.BuildProjection(e => new { e.Id, e.Name, e.Status }).ProjectionExpression,
                _ => throw new InvalidOperationException("Unexpected index")
            };

            if (string.IsNullOrEmpty(cachedResult))
            {
                throw new InvalidOperationException(
                    $"Scenario '{Name}' failed: cache entry {i} returned null or empty on re-read");
            }
        }
    }

    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }
}
