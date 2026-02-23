using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for UpdateExpressionBuilder — single through mixed clauses and functions.
/// PR-04.4
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class UpdateExpressionBenchmarks
{
    private IAttributeNameResolverFactory _resolverFactory = null!;
    private IAttributeValueConverterRegistry _converterRegistry = null!;

    [GlobalSetup]
    public void Setup()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;
    }

    // --- Complexity tiers ---

    [Benchmark(Baseline = true)]
    public UpdateExpressionResult SingleSet()
    {
        return new UpdateExpressionBuilder<BenchmarkOrder>(_resolverFactory, _converterRegistry)
            .Set(x => x.CustomerId, "CUST-999")
            .Build();
    }

    [Benchmark]
    public UpdateExpressionResult FiveSets()
    {
        return new UpdateExpressionBuilder<BenchmarkOrder>(_resolverFactory, _converterRegistry)
            .Set(x => x.CustomerId, "CUST-999")
            .Set(x => x.TotalAmount, 199.99m)
            .Set(x => x.Quantity, 5)
            .Set(x => x.IsActive, true)
            .Set(x => x.Score, 42)
            .Build();
    }

    [Benchmark]
    public UpdateExpressionResult MixedClauses()
    {
        return new UpdateExpressionBuilder<BenchmarkOrder>(_resolverFactory, _converterRegistry)
            .Set(x => x.CustomerId, "CUST-999")
            .Set(x => x.TotalAmount, 199.99m)
            .Remove(x => x.ShippedAt)
            .Add(x => x.Score, 10)
            .Build();
    }

    // --- DynamoDB functions ---

    [Benchmark]
    public UpdateExpressionResult WithFunctions()
    {
        return new UpdateExpressionBuilder<BenchmarkOrder>(_resolverFactory, _converterRegistry)
            .SetIfNotExists(x => x.CustomerId, "DEFAULT")
            .AppendToList(x => x.Tags, new List<string> { "new-tag" })
            .Build();
    }
}
