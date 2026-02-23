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
/// Benchmarks for ProjectionBuilder — cold/warm paths, varying property counts, reserved keywords.
/// PR-04.1
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ProjectionBuilderBenchmarks
{
    private IAttributeNameResolverFactory _resolverFactory = null!;

    // Cold-path builder: NullExpressionCache bypasses caching, measuring raw traversal
    private ProjectionBuilder<BenchmarkOrder> _coldBuilder = null!;

    // Warm-path builder: uses real cache, primed in GlobalSetup
    private ProjectionBuilder<BenchmarkOrder> _warmBuilder = null!;
    private ExpressionCache _warmCache = null!;

    // Pre-defined expressions (stable references for warm-path cache hits)
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
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();

        // Cold builder: no caching
        _coldBuilder = new ProjectionBuilder<BenchmarkOrder>(
            _resolverFactory,
            ReservedKeywordRegistry.Default,
            NullExpressionCache.Instance);

        // Warm builder: real cache, primed with all expressions
        _warmCache = new ExpressionCache();
        _warmBuilder = new ProjectionBuilder<BenchmarkOrder>(
            _resolverFactory,
            ReservedKeywordRegistry.Default,
            _warmCache);

        // Prime the cache
        _warmBuilder.BuildProjection(SinglePropExpr);
        _warmBuilder.BuildProjection(FivePropsExpr);
        _warmBuilder.BuildProjection(TwentyPropsExpr);
        _warmBuilder.BuildProjection(NestedExpr);
        _warmBuilder.BuildProjection(ReservedKeywordsExpr);
    }

    // --- Cold path: no cache, raw expression tree traversal ---

    [Benchmark(Baseline = true)]
    public ProjectionResult BuildProjection_Cold_SingleProperty()
        => _coldBuilder.BuildProjection(SinglePropExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_Cold_FiveProperties()
        => _coldBuilder.BuildProjection(FivePropsExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_Cold_TwentyProperties()
        => _coldBuilder.BuildProjection(TwentyPropsExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_Cold_NestedProperties()
        => _coldBuilder.BuildProjection(NestedExpr);

    // --- Warm path: cached result lookup ---

    [Benchmark]
    public ProjectionResult BuildProjection_Warm_SingleProperty()
        => _warmBuilder.BuildProjection(SinglePropExpr);

    [Benchmark]
    public ProjectionResult BuildProjection_Warm_TwentyProperties()
        => _warmBuilder.BuildProjection(TwentyPropsExpr);

    // --- Reserved keywords: alias generation overhead ---

    [Benchmark]
    public ProjectionResult BuildProjection_ReservedKeywords_Five()
        => _coldBuilder.BuildProjection(ReservedKeywordsExpr);
}
