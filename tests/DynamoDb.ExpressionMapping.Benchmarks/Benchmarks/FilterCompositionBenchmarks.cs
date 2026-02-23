using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for FilterExpressionResult.And/Or composition — re-aliasing overhead and chaining.
/// PR-04.3
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class FilterCompositionBenchmarks
{
    private FilterExpressionBuilder<BenchmarkOrder> _builder = null!;

    // Pre-built filter results for composition
    private FilterExpressionResult _simpleFilter1 = null!;
    private FilterExpressionResult _simpleFilter2 = null!;
    private FilterExpressionResult _complexFilter1 = null!;
    private FilterExpressionResult _complexFilter2 = null!;
    private FilterExpressionResult[] _fiveSimpleFilters = null!;

    private static readonly Expression<Func<BenchmarkOrder, bool>> SimpleExpr1 =
        x => x.OrderId == "ORD-001";

    private static readonly Expression<Func<BenchmarkOrder, bool>> SimpleExpr2 =
        x => x.IsActive && x.Quantity > 10;

    private static readonly Expression<Func<BenchmarkOrder, bool>> ComplexExpr1 =
        x => (x.IsActive && x.Quantity > 5) || (!x.IsActive && x.Score < 100);

    private static readonly Expression<Func<BenchmarkOrder, bool>> ComplexExpr2 =
        x => x.TotalAmount >= 99.99m && x.Priority == OrderPriority.High && x.Name != "VOID";

    [GlobalSetup]
    public void Setup()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _builder = new FilterExpressionBuilder<BenchmarkOrder>(resolverFactory, converterRegistry);

        _simpleFilter1 = _builder.BuildFilter(SimpleExpr1);
        _simpleFilter2 = _builder.BuildFilter(SimpleExpr2);
        _complexFilter1 = _builder.BuildFilter(ComplexExpr1);
        _complexFilter2 = _builder.BuildFilter(ComplexExpr2);

        // Build 5 distinct simple filters for chaining benchmark
        _fiveSimpleFilters =
        [
            _builder.BuildFilter(x => x.OrderId == "ORD-001"),
            _builder.BuildFilter(x => x.IsActive),
            _builder.BuildFilter(x => x.Quantity > 10),
            _builder.BuildFilter(x => x.TotalAmount >= 50m),
            _builder.BuildFilter(x => x.Score < 100),
        ];
    }

    // --- Composition: And ---

    [Benchmark(Baseline = true)]
    public FilterExpressionResult And_TwoSimpleFilters()
        => FilterExpressionResult.And(_simpleFilter1, _simpleFilter2);

    [Benchmark]
    public FilterExpressionResult And_TwoComplexFilters()
        => FilterExpressionResult.And(_complexFilter1, _complexFilter2);

    [Benchmark]
    public FilterExpressionResult Chain_FiveFilters_And()
    {
        var result = _fiveSimpleFilters[0];
        for (int i = 1; i < _fiveSimpleFilters.Length; i++)
        {
            result = FilterExpressionResult.And(result, _fiveSimpleFilters[i]);
        }
        return result;
    }

    // --- Composition: Or ---

    [Benchmark]
    public FilterExpressionResult Or_TwoFilters()
        => FilterExpressionResult.Or(_simpleFilter1, _simpleFilter2);
}
