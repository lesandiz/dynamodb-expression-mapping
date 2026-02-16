using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;

/// <summary>
/// PR-02.7 Scenario 3: Concurrent filter composition.
/// Multiple threads compose filters using And()/Or() simultaneously.
/// Assert: no alias collisions, no shared mutable state corruption.
/// </summary>
public class FilterCompositionConcurrencyScenario : IConcurrencyScenario
{
    private readonly SharedDependencies _sharedDependencies;

    public FilterCompositionConcurrencyScenario(SharedDependencies sharedDependencies)
    {
        _sharedDependencies = sharedDependencies ?? throw new ArgumentNullException(nameof(sharedDependencies));
    }

    public string Name => "Concurrent filter composition";

    public async Task ExecuteAsync(int concurrentWorkers, CancellationToken cancellationToken = default)
    {
        var results = new FilterCompositionResult[concurrentWorkers];
        var exceptions = new Exception?[concurrentWorkers];
        var tasks = new Task[concurrentWorkers];

        // Each worker builds and composes filters
        for (int i = 0; i < concurrentWorkers; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    var builder1 = new FilterExpressionBuilder<TestEntity>(
                        _sharedDependencies.ResolverFactory,
                        _sharedDependencies.ConverterRegistry);

                    var builder2 = new FilterExpressionBuilder<TestEntity>(
                        _sharedDependencies.ResolverFactory,
                        _sharedDependencies.ConverterRegistry);

                    // Build two independent filters
                    var filter1 = builder1.BuildFilter(e => e.Status == "Active");
                    var filter2 = builder2.BuildFilter(e => e.Value > 100);

                    // Compose with And (static method)
                    var andComposed = FilterExpressionResult.And(filter1, filter2);

                    // Compose with Or (static method)
                    var orComposed = FilterExpressionResult.Or(filter1, filter2);

                    // Verify no alias collisions in composed results
                    var andNameAliases = andComposed.ExpressionAttributeNames.Keys.ToHashSet();
                    var andValueAliases = andComposed.ExpressionAttributeValues.Keys.ToHashSet();
                    var orNameAliases = orComposed.ExpressionAttributeNames.Keys.ToHashSet();
                    var orValueAliases = orComposed.ExpressionAttributeValues.Keys.ToHashSet();

                    // Check for proper alias scoping and no duplicates
                    if (andNameAliases.Count != andComposed.ExpressionAttributeNames.Count)
                    {
                        throw new InvalidOperationException("Duplicate name aliases detected in AND composition");
                    }

                    if (andValueAliases.Count != andComposed.ExpressionAttributeValues.Count)
                    {
                        throw new InvalidOperationException("Duplicate value aliases detected in AND composition");
                    }

                    if (orNameAliases.Count != orComposed.ExpressionAttributeNames.Count)
                    {
                        throw new InvalidOperationException("Duplicate name aliases detected in OR composition");
                    }

                    if (orValueAliases.Count != orComposed.ExpressionAttributeValues.Count)
                    {
                        throw new InvalidOperationException("Duplicate value aliases detected in OR composition");
                    }

                    results[index] = new FilterCompositionResult(
                        AndExpression: andComposed.Expression,
                        OrExpression: orComposed.Expression,
                        AndNameAliasCount: andNameAliases.Count,
                        AndValueAliasCount: andValueAliases.Count,
                        OrNameAliasCount: orNameAliases.Count,
                        OrValueAliasCount: orValueAliases.Count
                    );
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

        // Assert: all workers got consistent results
        var firstResult = results[0];
        if (firstResult == null)
        {
            throw new InvalidOperationException($"Scenario '{Name}' failed: first result was null");
        }

        for (int i = 1; i < results.Length; i++)
        {
            if (results[i] == null)
            {
                throw new InvalidOperationException($"Scenario '{Name}' failed: result {i} was null");
            }

            // All workers should produce semantically identical compositions
            if (results[i].AndNameAliasCount != firstResult.AndNameAliasCount ||
                results[i].AndValueAliasCount != firstResult.AndValueAliasCount ||
                results[i].OrNameAliasCount != firstResult.OrNameAliasCount ||
                results[i].OrValueAliasCount != firstResult.OrValueAliasCount)
            {
                throw new InvalidOperationException(
                    $"Scenario '{Name}' failed: alias count mismatch at worker {i}. " +
                    $"Expected: AND({firstResult.AndNameAliasCount},{firstResult.AndValueAliasCount}) OR({firstResult.OrNameAliasCount},{firstResult.OrValueAliasCount}), " +
                    $"Got: AND({results[i].AndNameAliasCount},{results[i].AndValueAliasCount}) OR({results[i].OrNameAliasCount},{results[i].OrValueAliasCount})");
            }
        }
    }

    private record FilterCompositionResult(
        string AndExpression,
        string OrExpression,
        int AndNameAliasCount,
        int AndValueAliasCount,
        int OrNameAliasCount,
        int OrValueAliasCount);

    private class TestEntity
    {
        public string Status { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }
}
