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

    // Pre-defined expressions
    private static readonly Expression<Func<BenchmarkOrder, bool>> SimpleEqualityExpr =
        x => x.OrderId == "ORD-001";

    private static readonly Expression<Func<BenchmarkOrder, bool>> ThreePredicatesAndExpr =
        x => x.IsActive && x.Quantity > 10 && x.TotalAmount >= 99.99m;

    private static readonly Expression<Func<BenchmarkOrder, bool>> ComplexPredicateExpr =
        x => (x.IsActive && x.Quantity > 5) || (!x.IsActive && x.Score < 100);

    private static readonly Expression<Func<BenchmarkOrder, bool>> NestedPropertyExpr =
        x => x.Address!.City == "London";

    private static readonly Expression<Func<BenchmarkOrder, bool>> StringMethodExpr =
        x => x.Name.Contains("test") && x.OrderId.StartsWith("ORD");

    // Pre-built closure expressions — built in GlobalSetup so the expression tree
    // allocation is excluded from per-invocation measurement
    private Expression<Func<BenchmarkOrder, bool>> _capturedStringExpr = null!;
    private Expression<Func<BenchmarkOrder, bool>> _capturedEnumExpr = null!;
    private Expression<Func<BenchmarkOrder, bool>> _capturedDateTimeExpr = null!;

    [GlobalSetup]
    public void Setup()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _builder = new FilterExpressionBuilder<BenchmarkOrder>(resolverFactory, converterRegistry);

        // Build closure expressions with captured variables — same MemberAccess
        // structure as inline lambdas but allocated once
        var capturedString = "CUST-12345";
        var capturedEnum = OrderPriority.High;
        var capturedDateTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        _capturedStringExpr = x => x.CustomerId == capturedString;
        _capturedEnumExpr = x => x.Priority == capturedEnum;
        _capturedDateTimeExpr = x => x.CreatedAt > capturedDateTime;
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

    // --- Method call visitor path (contains / begins_with) ---

    [Benchmark]
    public FilterExpressionResult StringMethods()
        => _builder.BuildFilter(StringMethodExpr);

    // --- Captured variable evaluation ---

    [Benchmark]
    public FilterExpressionResult CapturedVariable_String()
        => _builder.BuildFilter(_capturedStringExpr);

    [Benchmark]
    public FilterExpressionResult CapturedVariable_Enum()
        => _builder.BuildFilter(_capturedEnumExpr);

    [Benchmark]
    public FilterExpressionResult CapturedVariable_DateTime()
        => _builder.BuildFilter(_capturedDateTimeExpr);
}
