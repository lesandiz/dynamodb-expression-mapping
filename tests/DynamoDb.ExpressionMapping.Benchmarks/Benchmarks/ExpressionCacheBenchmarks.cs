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
/// Benchmarks for ExpressionCache — cache lookup performance at various sizes.
/// CacheMiss is included as a control to validate that miss cost is constant across cache sizes.
/// PR-04.7
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ExpressionCacheLookupBenchmarks
{
    private ExpressionCache _cache = null!;
    private ProjectionBuilder<BenchmarkOrder> _hitBuilder = null!;
    private ProjectionBuilder<BenchmarkOrder> _missBuilder = null!;

    // The expression whose key is always present in the cache (hit target)
    private static readonly Expression<Func<BenchmarkOrder, object>> HitExpr =
        o => new { o.OrderId, o.CustomerId, o.TotalAmount };

    // An expression whose key is never in the cache (miss target).
    // Uses NullExpressionCache so the factory is always invoked without growing the cache.
    private static readonly Expression<Func<BenchmarkOrder, object>> MissExpr =
        o => new { o.Score, o.Prop8, o.Prop7, o.Prop6, o.Prop5 };

    /// <summary>
    /// Number of entries pre-populated in the cache before benchmarks run.
    /// Measures lookup performance as the underlying ConcurrentDictionary grows.
    /// </summary>
    [Params(10, 100, 1000)]
    public int CacheSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _cache = new ExpressionCache();
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();

        _hitBuilder = new ProjectionBuilder<BenchmarkOrder>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            _cache);

        // Miss builder uses NullExpressionCache — always invokes factory, no cache pollution
        _missBuilder = new ProjectionBuilder<BenchmarkOrder>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            NullExpressionCache.Instance);

        // Prime the hit expression so it's always present
        _hitBuilder.BuildProjection(HitExpr);

        // Fill the cache with synthetic entries to reach CacheSize.
        // Uses the raw cache API with unique keys to simulate a populated cache.
        for (int i = 1; i < CacheSize; i++)
        {
            _cache.GetOrAdd("projection", $"synthetic-key-{i}", _ => ProjectionResult.Empty);
        }
    }

    [Benchmark(Baseline = true)]
    public ProjectionResult CacheHit()
        => _hitBuilder.BuildProjection(HitExpr);

    [Benchmark]
    public ProjectionResult CacheMiss()
        => _missBuilder.BuildProjection(MissExpr);
}

/// <summary>
/// Benchmarks for ExpressionKeyGenerator — key generation overhead independent of cache size.
/// PR-04.7
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class ExpressionKeyGeneratorBenchmarks
{
    private static readonly Expression<Func<BenchmarkOrder, object>> SimpleKeyExpr =
        o => o.OrderId;

    private static readonly Expression<Func<BenchmarkOrder, object>> ComplexKeyExpr =
        o => new
        {
            o.OrderId, o.CustomerId, o.Name, o.Status,
            o.TotalAmount, o.Quantity, o.IsActive, o.CreatedAt,
            o.ShippedAt, o.Priority, o.Score,
            City = o.Address!.City, Zip = o.Address.ZipCode
        };

    [Benchmark(Baseline = true)]
    public string GenerateKey_SimpleExpression()
        => ExpressionKeyGenerator.GenerateKey(SimpleKeyExpr);

    [Benchmark]
    public string GenerateKey_ComplexExpression()
        => ExpressionKeyGenerator.GenerateKey(ComplexKeyExpr);
}
