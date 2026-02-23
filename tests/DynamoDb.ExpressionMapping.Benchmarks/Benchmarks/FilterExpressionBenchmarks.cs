using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for FilterExpressionBuilder — complexity tiers and captured variable evaluation.
/// PR-04.2
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class FilterExpressionBenchmarks
{
    private FilterExpressionBuilder<BenchmarkOrder> _builder = null!;

    // Captured variables for closure benchmarks
    private string _capturedString = "CUST-12345";
    private OrderPriority _capturedEnum = OrderPriority.High;
    private DateTime _capturedDateTime = new(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    // Pre-defined expressions
    private static readonly Expression<Func<BenchmarkOrder, bool>> SimpleEqualityExpr =
        x => x.OrderId == "ORD-001";

    private static readonly Expression<Func<BenchmarkOrder, bool>> ThreePredicatesAndExpr =
        x => x.IsActive && x.Quantity > 10 && x.TotalAmount >= 99.99m;

    private static readonly Expression<Func<BenchmarkOrder, bool>> ComplexPredicateExpr =
        x => (x.IsActive && x.Quantity > 5) || (!x.IsActive && x.Score < 100);

    private static readonly Expression<Func<BenchmarkOrder, bool>> NestedPropertyExpr =
        x => x.Address!.City == "London";

    [GlobalSetup]
    public void Setup()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _builder = new FilterExpressionBuilder<BenchmarkOrder>(resolverFactory, converterRegistry);
    }

    // --- Complexity tiers ---

    [Benchmark(Baseline = true)]
    public FilterExpressionResult SimpleEquality()
        => _builder.BuildFilter(SimpleEqualityExpr);

    [Benchmark]
    public FilterExpressionResult ThreePredicates_And()
        => _builder.BuildFilter(ThreePredicatesAndExpr);

    [Benchmark]
    public FilterExpressionResult ComplexPredicate()
        => _builder.BuildFilter(ComplexPredicateExpr);

    [Benchmark]
    public FilterExpressionResult WithNestedProperty()
        => _builder.BuildFilter(NestedPropertyExpr);

    // --- Captured variable evaluation ---

    [Benchmark]
    public FilterExpressionResult CapturedVariable_String()
        => _builder.BuildFilter(x => x.CustomerId == _capturedString);

    [Benchmark]
    public FilterExpressionResult CapturedVariable_Enum()
        => _builder.BuildFilter(x => x.Priority == _capturedEnum);

    [Benchmark]
    public FilterExpressionResult CapturedVariable_DateTime()
        => _builder.BuildFilter(x => x.CreatedAt > _capturedDateTime);
}
