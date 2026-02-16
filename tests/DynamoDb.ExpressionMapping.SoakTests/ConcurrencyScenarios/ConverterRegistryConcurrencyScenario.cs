using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.SoakTests.ConcurrencyScenarios;

/// <summary>
/// PR-02.7 Scenario 4: Concurrent converter registry reads.
/// Multiple threads resolve converters for different types simultaneously.
/// Assert: correct converter returned for each type, no exceptions.
/// </summary>
public class ConverterRegistryConcurrencyScenario : IConcurrencyScenario
{
    private readonly SharedDependencies _sharedDependencies;

    public ConverterRegistryConcurrencyScenario(SharedDependencies sharedDependencies)
    {
        _sharedDependencies = sharedDependencies ?? throw new ArgumentNullException(nameof(sharedDependencies));
    }

    public string Name => "Concurrent converter registry reads";

    public async Task ExecuteAsync(int concurrentWorkers, CancellationToken cancellationToken = default)
    {
        var results = new ConverterResolutionResult[concurrentWorkers];
        var exceptions = new Exception?[concurrentWorkers];
        var tasks = new Task[concurrentWorkers];

        // Each worker resolves converters for different types
        for (int i = 0; i < concurrentWorkers; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    var typeToResolve = index % 8;

                    // Resolve different types based on worker index
                    var (converterType, roundTripValue) = typeToResolve switch
                    {
                        0 => ResolveAndTestConverter<string>("test-string"),
                        1 => ResolveAndTestConverter<int>(42),
                        2 => ResolveAndTestConverter<decimal>(123.45m),
                        3 => ResolveAndTestConverter<bool>(true),
                        4 => ResolveAndTestConverter<DateTime>(DateTime.UtcNow),
                        5 => ResolveAndTestConverter<Guid>(Guid.NewGuid()),
                        6 => ResolveAndTestConverter<int?>(100),
                        7 => ResolveAndTestConverter<TestEnum>(TestEnum.Active),
                        _ => throw new InvalidOperationException("Unexpected type index")
                    };

                    results[index] = new ConverterResolutionResult(
                        ConverterTypeName: converterType,
                        RoundTripSucceeded: roundTripValue
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

        // Assert: all workers successfully resolved converters
        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] == null)
            {
                throw new InvalidOperationException($"Scenario '{Name}' failed: result {i} was null");
            }

            if (string.IsNullOrEmpty(results[i].ConverterTypeName))
            {
                throw new InvalidOperationException(
                    $"Scenario '{Name}' failed: worker {i} did not resolve a converter");
            }

            if (!results[i].RoundTripSucceeded)
            {
                throw new InvalidOperationException(
                    $"Scenario '{Name}' failed: worker {i} round-trip conversion failed");
            }
        }
    }

    private (string converterTypeName, bool roundTripSucceeded) ResolveAndTestConverter<T>(T testValue)
    {
        // Resolve converter using GetConverter method
        var converter = _sharedDependencies.ConverterRegistry.GetConverter(typeof(T));

        if (converter == null)
        {
            throw new InvalidOperationException($"No converter found for type {typeof(T).Name}");
        }

        // Test round-trip conversion
        var attributeValue = converter.ToAttributeValue(testValue!);
        if (attributeValue == null)
        {
            throw new InvalidOperationException($"Converter for {typeof(T).Name} returned null AttributeValue");
        }

        var roundTripped = converter.FromAttributeValue(attributeValue);

        // For value types and strings, verify equality
        bool roundTripSuccess = typeof(T).IsValueType || typeof(T) == typeof(string)
            ? Equals(testValue, roundTripped)
            : roundTripped != null;

        return (converter.GetType().Name, roundTripSuccess);
    }

    private record ConverterResolutionResult(
        string ConverterTypeName,
        bool RoundTripSucceeded);

    private enum TestEnum
    {
        Active,
        Inactive
    }
}
