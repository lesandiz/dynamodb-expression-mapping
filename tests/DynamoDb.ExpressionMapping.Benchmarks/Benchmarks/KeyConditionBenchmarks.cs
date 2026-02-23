using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for KeyConditionExpressionBuilder — partition-only, comparison operators,
/// BETWEEN, begins_with, and reserved keyword aliasing.
/// PR-04.8
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class KeyConditionBenchmarks
{
    private KeyConditionExpressionBuilder<BenchmarkOrder> _builder = null!;

    [GlobalSetup]
    public void Setup()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _builder = new KeyConditionExpressionBuilder<BenchmarkOrder>(resolverFactory, converterRegistry);
    }

    // --- Partition key only ---

    [Benchmark(Baseline = true)]
    public KeyConditionExpressionResult PartitionKeyOnly()
        => _builder
            .WithPartitionKey(o => o.PK, "USER#123")
            .Build();

    // --- Partition + Sort key comparisons ---

    [Benchmark]
    public KeyConditionExpressionResult PartitionAndSortKey_Equals()
        => _builder
            .WithPartitionKey(o => o.PK, "USER#123")
            .WithSortKeyEquals(o => o.SK, "ORDER#456");

    [Benchmark]
    public KeyConditionExpressionResult PartitionAndSortKey_GreaterThan()
        => _builder
            .WithPartitionKey(o => o.PK, "USER#123")
            .WithSortKeyGreaterThan(o => o.SK, "ORDER#100");

    [Benchmark]
    public KeyConditionExpressionResult PartitionAndSortKey_LessThanOrEqual()
        => _builder
            .WithPartitionKey(o => o.PK, "USER#123")
            .WithSortKeyLessThanOrEqual(o => o.SK, "ORDER#999");

    // --- BETWEEN and begins_with ---

    [Benchmark]
    public KeyConditionExpressionResult SortKey_Between()
        => _builder
            .WithPartitionKey(o => o.PK, "USER#123")
            .WithSortKeyBetween(o => o.SK, "ORDER#100", "ORDER#999");

    [Benchmark]
    public KeyConditionExpressionResult SortKey_BeginsWith()
        => _builder
            .WithPartitionKey(o => o.PK, "USER#123")
            .WithSortKeyBeginsWith(o => o.SK, "ORDER#");

    // --- Reserved keyword aliasing overhead ---

    [Benchmark]
    public KeyConditionExpressionResult ReservedKeyword_PartitionKey()
        => _builder
            .WithPartitionKey(o => o.Name, "Reserved")
            .Build();

    [Benchmark]
    public KeyConditionExpressionResult ReservedKeyword_BothKeys()
        => _builder
            .WithPartitionKey(o => o.Name, "Reserved")
            .WithSortKeyEquals(o => o.Status, "Active");
}
