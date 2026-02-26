using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for ProjectionBuilder — cold path (no cache, raw expression tree traversal)
/// and reserved keyword aliasing overhead.
/// PR-04.1
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ProjectionBuilderColdBenchmarks
{
    private ProjectionBuilder<BenchmarkOrder> _coldBuilder = null!;

    private static readonly Expression<Func<BenchmarkOrder, object>> SinglePropExpr =
        o => o.OrderId;

    private static readonly Expression<Func<BenchmarkOrder, object>> FivePropsExpr =
        o => new { o.OrderId, o.CustomerId, o.TotalAmount, o.Quantity, o.IsActive };

    private static readonly Expression<Func<BenchmarkOrder, object>> TwentyPropsExpr =
        o => new
        {
            o.OrderId, o.CustomerId, o.Name, o.Status,
            o.TotalAmount, o.Quantity, o.IsActive, o.CreatedAt,
            o.ShippedAt, o.Priority, o.Score,
            o.Prop1, o.Prop2, o.Prop3, o.Prop4,
            o.Prop5, o.Prop6, o.Prop7, o.Prop8,
            o.PK
        };

    private static readonly Expression<Func<BenchmarkOrder, object>> NestedExpr =
        o => new { o.OrderId, City = o.Address!.City, Zip = o.Address.ZipCode };

    private static readonly Expression<Func<BenchmarkOrder, object>> ReservedKeywordsExpr =
        o => new { o.OrderId, o.Name, o.Status, o.CreatedAt, o.Priority };

    [GlobalSetup]
    public void Setup()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();

        _coldBuilder = new ProjectionBuilder<BenchmarkOrder>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            NullExpressionCache.Instance);
    }

    [Benchmark(Baseline = true)]
    public ProjectionResult BuildProjection_SingleProperty()
        => _coldBuilder.BuildProjection(SinglePropExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_FiveProperties()
        => _coldBuilder.BuildProjection(FivePropsExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_TwentyProperties()
        => _coldBuilder.BuildProjection(TwentyPropsExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_NestedProperties()
        => _coldBuilder.BuildProjection(NestedExpr);

    // --- Reserved keywords: alias generation overhead ---

    [Benchmark]
    public ProjectionResult BuildProjection_ReservedKeywords_Five()
        => _coldBuilder.BuildProjection(ReservedKeywordsExpr);
}

/// <summary>
/// Benchmarks for ProjectionBuilder — warm path (cached result lookup).
/// PR-04.1
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ProjectionBuilderWarmBenchmarks
{
    private ProjectionBuilder<BenchmarkOrder> _warmBuilder = null!;

    private static readonly Expression<Func<BenchmarkOrder, object>> SinglePropExpr =
        o => o.OrderId;

    private static readonly Expression<Func<BenchmarkOrder, object>> TwentyPropsExpr =
        o => new
        {
            o.OrderId, o.CustomerId, o.Name, o.Status,
            o.TotalAmount, o.Quantity, o.IsActive, o.CreatedAt,
            o.ShippedAt, o.Priority, o.Score,
            o.Prop1, o.Prop2, o.Prop3, o.Prop4,
            o.Prop5, o.Prop6, o.Prop7, o.Prop8,
            o.PK
        };

    [GlobalSetup]
    public void Setup()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var warmCache = new ExpressionCache();

        _warmBuilder = new ProjectionBuilder<BenchmarkOrder>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            warmCache);

        // Prime the cache
        _warmBuilder.BuildProjection(SinglePropExpr);
        _warmBuilder.BuildProjection(TwentyPropsExpr);
    }

    [Benchmark(Baseline = true)]
    public ProjectionResult BuildProjection_SingleProperty()
        => _warmBuilder.BuildProjection(SinglePropExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_TwentyProperties()
        => _warmBuilder.BuildProjection(TwentyPropsExpr);
}
