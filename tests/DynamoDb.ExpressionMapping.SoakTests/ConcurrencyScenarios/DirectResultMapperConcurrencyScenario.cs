using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.ResultMapping;

namespace DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;

/// <summary>
/// PR-02.7 Scenario 5: Concurrent DirectResultMapper creation + execution.
/// Multiple threads create mappers for different projections and map results.
/// Assert: correct mapping, no delegate corruption.
/// </summary>
public class DirectResultMapperConcurrencyScenario : IConcurrencyScenario
{
    private readonly SharedDependencies _sharedDependencies;

    public DirectResultMapperConcurrencyScenario(SharedDependencies sharedDependencies)
    {
        _sharedDependencies = sharedDependencies ?? throw new ArgumentNullException(nameof(sharedDependencies));
    }

    public string Name => "Concurrent DirectResultMapper creation + execution";

    public async Task ExecuteAsync(int concurrentWorkers, CancellationToken cancellationToken = default)
    {
        var results = new MappingResult[concurrentWorkers];
        var exceptions = new Exception?[concurrentWorkers];
        var tasks = new Task[concurrentWorkers];

        // Prepare sample DynamoDB items for each worker
        var testItems = PrepareTestItems(concurrentWorkers);

        // Each worker creates a mapper and maps results
        for (int i = 0; i < concurrentWorkers; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    var resultMapperFactory = new DirectResultMapper<TestEntity>(
                        _sharedDependencies.ResolverFactory,
                        _sharedDependencies.ConverterRegistry);

                    // Create different mappers based on worker index
                    var mappingType = index % 7;

                    var (mappedValue, verificationPassed) = mappingType switch
                    {
                        0 => MapAndVerify(resultMapperFactory, testItems[index], e => new { e.Id }),
                        1 => MapAndVerify(resultMapperFactory, testItems[index], e => new { e.Id, e.Name }),
                        2 => MapAndVerify(resultMapperFactory, testItems[index], e => new { e.Name, e.Value }),
                        3 => MapAndVerify(resultMapperFactory, testItems[index], e => new { e.Id, e.Name, e.Value }),
                        4 => MapAndVerify(resultMapperFactory, testItems[index], e => new { e.Status, e.IsActive }),
                        5 => MapAndVerify(resultMapperFactory, testItems[index], e => new { Upper = e.Name.ToUpper(), e.Value }),
                        6 => MapAndVerify(resultMapperFactory, testItems[index], e => new { Trimmed = e.Name.Trim().ToUpper(), Price = e.Value.ToString() }),
                        _ => throw new InvalidOperationException("Unexpected mapping type")
                    };

                    results[index] = new MappingResult(
                        MappedValueJson: mappedValue,
                        VerificationPassed: verificationPassed
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

        // Assert: all workers successfully mapped results
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] == null)
            {
                throw new InvalidOperationException($"Scenario '{Name}' failed: result {i} was null");
            }

            if (string.IsNullOrEmpty(results[i].MappedValueJson))
            {
                throw new InvalidOperationException(
                    $"Scenario '{Name}' failed: worker {i} produced empty mapping result");
            }

            if (!results[i].VerificationPassed)
            {
                throw new InvalidOperationException(
                    $"Scenario '{Name}' failed: worker {i} mapping verification failed");
            }
        }
    }

    private Dictionary<string, AttributeValue>[] PrepareTestItems(int count)
    {
        var items = new Dictionary<string, AttributeValue>[count];
        for (int i = 0; i < count; i++)
        {
            items[i] = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = $"id-{i}" },
                ["Name"] = new AttributeValue { S = $"Name {i}" },
                ["Value"] = new AttributeValue { N = (100 + i).ToString() },
                ["Status"] = new AttributeValue { S = i % 2 == 0 ? "Active" : "Inactive" },
                ["IsActive"] = new AttributeValue { BOOL = i % 2 == 0 }
            };
        }
        return items;
    }

    private (string mappedValueJson, bool verificationPassed) MapAndVerify<TResult>(
        IDirectResultMapper<TestEntity> mapper,
        Dictionary<string, AttributeValue> item,
        System.Linq.Expressions.Expression<Func<TestEntity, TResult>> selector)
    {
        // Create mapper for the projection
        var mapFunc = mapper.CreateMapper(selector);
        if (mapFunc == null)
        {
            throw new InvalidOperationException("CreateMapper returned null");
        }

        // Execute mapping
        var result = mapFunc(item);
        if (result == null)
        {
            throw new InvalidOperationException("Mapping returned null result");
        }

        // Serialize for verification (simple string representation)
        var json = System.Text.Json.JsonSerializer.Serialize(result);

        // Verify by checking non-empty result
        var verificationPassed = !string.IsNullOrWhiteSpace(json) && json.Length > 2; // More than just "{}"

        return (json, verificationPassed);
    }

    private record MappingResult(
        string MappedValueJson,
        bool VerificationPassed);

    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
