using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for DirectResultMapper — delegate compilation (cold), mapping execution (warm),
/// and comparison against hand-written mapping code.
/// PR-04.5
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class DirectResultMapperBenchmarks
{
    private IAttributeNameResolverFactory _resolverFactory = null!;
    private IAttributeValueConverterRegistry _converterRegistry = null!;

    // Cold mapper: no internal caching (new instance per benchmark iteration group)
    // Warm mapper: pre-compiled delegates cached internally
    private DirectResultMapper<BenchmarkOrder> _warmMapper = null!;

    // Pre-compiled delegates for warm-path benchmarks
    private Func<Dictionary<string, AttributeValue>, object> _anonymousThreePropsDelegate = null!;
    private Func<Dictionary<string, AttributeValue>, OrderDetail> _namedTenPropsDelegate = null!;
    private Func<Dictionary<string, AttributeValue>, object> _nestedTypeDelegate = null!;

    // Pre-built attribute dictionaries
    private Dictionary<string, AttributeValue> _threePropsAttrs = null!;
    private Dictionary<string, AttributeValue> _fivePropsAttrs = null!;
    private Dictionary<string, AttributeValue> _tenPropsAttrs = null!;
    private Dictionary<string, AttributeValue> _nestedAttrs = null!;

    // Pre-defined expressions (stable references)
    private static readonly Expression<Func<BenchmarkOrder, object>> AnonymousThreePropsExpr =
        o => new { o.OrderId, o.Name, o.TotalAmount };

    private static readonly Expression<Func<BenchmarkOrder, OrderSummary>> NamedFivePropsExpr =
        o => new OrderSummary
        {
            OrderId = o.OrderId,
            Name = o.Name,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            City = o.Address!.City
        };

    private static readonly Expression<Func<BenchmarkOrder, OrderRecord>> RecordExpr =
        o => new OrderRecord(o.OrderId, o.Name, o.TotalAmount);

    private static readonly Expression<Func<BenchmarkOrder, OrderDetail>> NamedTenPropsExpr =
        o => new OrderDetail
        {
            OrderId = o.OrderId,
            CustomerId = o.CustomerId,
            Name = o.Name,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            Quantity = o.Quantity,
            IsActive = o.IsActive,
            CreatedAt = o.CreatedAt,
            Score = o.Score,
            Prop1 = o.Prop1
        };

    private static readonly Expression<Func<BenchmarkOrder, object>> NestedTypeExpr =
        o => new { o.OrderId, City = o.Address!.City, Zip = o.Address.ZipCode, CountryCode = o.Address.Country!.Code };

    [GlobalSetup]
    public void Setup()
    {
        _resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _converterRegistry = AttributeValueConverterRegistry.Default;

        // Warm mapper with pre-compiled delegates
        _warmMapper = new DirectResultMapper<BenchmarkOrder>(
            _resolverFactory,
            _converterRegistry);

        // Build attribute dictionaries
        _threePropsAttrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "ORD-001" },
            ["Name"] = new() { S = "Test Order" },
            ["TotalAmount"] = new() { N = "199.99" }
        };

        _fivePropsAttrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "ORD-002" },
            ["Name"] = new() { S = "Five Prop Order" },
            ["Status"] = new() { S = "Active" },
            ["TotalAmount"] = new() { N = "299.99" },
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["City"] = new() { S = "London" }
                }
            }
        };

        _tenPropsAttrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "ORD-003" },
            ["CustomerId"] = new() { S = "CUST-001" },
            ["Name"] = new() { S = "Ten Prop Order" },
            ["Status"] = new() { S = "Shipped" },
            ["TotalAmount"] = new() { N = "499.99" },
            ["Quantity"] = new() { N = "5" },
            ["IsActive"] = new() { BOOL = true },
            ["CreatedAt"] = new() { S = "2024-06-15T12:00:00.0000000Z" },
            ["Score"] = new() { N = "85" },
            ["Prop1"] = new() { S = "extra-1" }
        };

        _nestedAttrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "ORD-004" },
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["City"] = new() { S = "New York" },
                    ["ZipCode"] = new() { S = "10001" },
                    ["Country"] = new()
                    {
                        M = new Dictionary<string, AttributeValue>
                        {
                            ["Code"] = new() { S = "US" },
                            ["Name"] = new() { S = "United States" }
                        }
                    }
                }
            }
        };

        // Prime warm mapper by creating and caching delegates
        _anonymousThreePropsDelegate = _warmMapper.CreateMapper(AnonymousThreePropsExpr);
        _namedTenPropsDelegate = _warmMapper.CreateMapper(NamedTenPropsExpr);
        _nestedTypeDelegate = _warmMapper.CreateMapper(NestedTypeExpr);
    }

    // --- Cold path: delegate compilation overhead ---

    [Benchmark]
    public Func<Dictionary<string, AttributeValue>, object> CreateMapper_AnonymousType()
    {
        var mapper = new DirectResultMapper<BenchmarkOrder>(_resolverFactory, _converterRegistry);
        return mapper.CreateMapper(AnonymousThreePropsExpr);
    }

    [Benchmark]
    public Func<Dictionary<string, AttributeValue>, OrderSummary> CreateMapper_NamedType_FiveProps()
    {
        var mapper = new DirectResultMapper<BenchmarkOrder>(_resolverFactory, _converterRegistry);
        return mapper.CreateMapper(NamedFivePropsExpr);
    }

    [Benchmark]
    public Func<Dictionary<string, AttributeValue>, OrderRecord> CreateMapper_Record()
    {
        var mapper = new DirectResultMapper<BenchmarkOrder>(_resolverFactory, _converterRegistry);
        return mapper.CreateMapper(RecordExpr);
    }

    // --- Warm path: mapping execution using pre-compiled delegates ---

    [Benchmark(Baseline = true)]
    public object Map_AnonymousType_ThreeProps()
        => _anonymousThreePropsDelegate(_threePropsAttrs);

    [Benchmark]
    public OrderDetail Map_NamedType_TenProps()
        => _namedTenPropsDelegate(_tenPropsAttrs);

    [Benchmark]
    public object Map_NestedType()
        => _nestedTypeDelegate(_nestedAttrs);

    // --- Comparison: hand-written mapping baseline ---

    [Benchmark]
    public OrderDetail Map_Manual_Baseline()
    {
        var attrs = _tenPropsAttrs;
        return new OrderDetail
        {
            OrderId = attrs.TryGetValue("OrderId", out var orderId) ? orderId.S : default!,
            CustomerId = attrs.TryGetValue("CustomerId", out var custId) ? custId.S : default!,
            Name = attrs.TryGetValue("Name", out var name) ? name.S : default!,
            Status = attrs.TryGetValue("Status", out var status) ? status.S : default!,
            TotalAmount = attrs.TryGetValue("TotalAmount", out var total) && total.N != null
                ? decimal.Parse(total.N)
                : 0m,
            Quantity = attrs.TryGetValue("Quantity", out var qty) && qty.N != null
                ? int.Parse(qty.N)
                : 0,
            IsActive = attrs.TryGetValue("IsActive", out var active) && active.BOOL,
            CreatedAt = attrs.TryGetValue("CreatedAt", out var created) && created.S != null
                ? DateTime.Parse(created.S)
                : default,
            Score = attrs.TryGetValue("Score", out var score) && score.N != null
                ? int.Parse(score.N)
                : 0,
            Prop1 = attrs.TryGetValue("Prop1", out var p1) ? p1.S : default!
        };
    }
}
