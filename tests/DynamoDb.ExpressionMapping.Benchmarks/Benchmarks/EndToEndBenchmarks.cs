using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using DynamoDb.ExpressionMapping.Benchmarks.Fixtures;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.ResultMapping;

namespace DynamoDb.ExpressionMapping.Benchmarks.Benchmarks;

/// <summary>
/// End-to-end benchmarks measuring the full pipeline:
/// build expressions → apply to QueryRequest → map results.
/// Compares cold (no cache) vs warm (cached) paths.
/// PR-04.8
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
public class EndToEndBenchmarks
{
    // Cold-path builders: NullExpressionCache bypasses caching
    private ProjectionBuilder<BenchmarkOrder> _coldProjectionBuilder = null!;
    private FilterExpressionBuilder<BenchmarkOrder> _coldFilterBuilder = null!;
    private KeyConditionExpressionBuilder<BenchmarkOrder> _keyConditionBuilder = null!;
    private DirectResultMapper<BenchmarkOrder> _resultMapper = null!;

    // Warm-path builders: real cache, primed in GlobalSetup
    private ProjectionBuilder<BenchmarkOrder> _warmProjectionBuilder = null!;
    private FilterExpressionBuilder<BenchmarkOrder> _warmFilterBuilder = null!;

    // Pre-compiled mapper delegate for warm mapping
    private Func<Dictionary<string, AttributeValue>, OrderSummary> _warmMapperDelegate = null!;

    // Simulated DynamoDB response attributes
    private Dictionary<string, AttributeValue> _responseAttrs = null!;

    // Pre-defined expressions (stable references for cache hits)
    private static readonly Expression<Func<BenchmarkOrder, OrderSummary>> ProjectionExpr =
        o => new OrderSummary
        {
            OrderId = o.OrderId,
            Name = o.Name,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            City = o.Address!.City
        };

    private static readonly Expression<Func<BenchmarkOrder, bool>> FilterExpr =
        x => x.IsActive && x.TotalAmount >= 99.99m && x.Priority == OrderPriority.High;

    [GlobalSetup]
    public void Setup()
    {
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        // Cold builders — no caching
        _coldProjectionBuilder = new ProjectionBuilder<BenchmarkOrder>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            NullExpressionCache.Instance);

        _coldFilterBuilder = new FilterExpressionBuilder<BenchmarkOrder>(resolverFactory, converterRegistry);

        _keyConditionBuilder = new KeyConditionExpressionBuilder<BenchmarkOrder>(resolverFactory, converterRegistry);

        _resultMapper = new DirectResultMapper<BenchmarkOrder>(resolverFactory, converterRegistry);

        // Warm builders — real cache, primed
        var warmCache = new ExpressionCache();
        _warmProjectionBuilder = new ProjectionBuilder<BenchmarkOrder>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            warmCache);
        _warmFilterBuilder = new FilterExpressionBuilder<BenchmarkOrder>(resolverFactory, converterRegistry);

        // Prime the warm cache
        _warmProjectionBuilder.BuildProjection(ProjectionExpr);
        _warmFilterBuilder.BuildFilter(FilterExpr);

        // Pre-compile mapper delegate for warm mapping benchmarks
        _warmMapperDelegate = _resultMapper.CreateMapper(ProjectionExpr);

        // Simulated response from DynamoDB
        _responseAttrs = new Dictionary<string, AttributeValue>
        {
            ["OrderId"] = new() { S = "ORD-001" },
            ["Name"] = new() { S = "Test Order" },
            ["Status"] = new() { S = "Shipped" },
            ["TotalAmount"] = new() { N = "199.99" },
            ["Address"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["City"] = new() { S = "London" }
                }
            }
        };
    }

    // --- Build only: construct expressions without applying to a request ---

    [Benchmark]
    public object QueryWithProjectionAndFilter_BuildOnly()
    {
        var projection = _coldProjectionBuilder.BuildProjection(ProjectionExpr);
        var filter = _coldFilterBuilder.BuildFilter(FilterExpr);
        var keyCondition = _keyConditionBuilder
            .WithPartitionKey(o => o.PK, "USER#123")
            .WithSortKeyBeginsWith(o => o.SK, "ORDER#");

        return (projection, filter, keyCondition);
    }

    // --- Build + Apply: construct and apply expressions to a QueryRequest ---

    [Benchmark]
    public QueryRequest QueryWithProjectionAndFilter_BuildAndApply()
    {
        return new QueryRequest { TableName = "Orders" }
            .WithProjection(_coldProjectionBuilder, ProjectionExpr)
            .WithFilter(_coldFilterBuilder, FilterExpr)
            .WithKeyCondition(_keyConditionBuilder,
                b => b.WithPartitionKey(o => o.PK, "USER#123")
                       .WithSortKeyBeginsWith(o => o.SK, "ORDER#"));
    }

    // --- Full pipeline cold: build + apply + map result (no caching) ---

    [Benchmark(Baseline = true)]
    public (QueryRequest, OrderSummary) FullPipeline_Cold()
    {
        // Build and apply expressions via public extension methods
        var request = new QueryRequest { TableName = "Orders" }
            .WithProjection(_coldProjectionBuilder, ProjectionExpr)
            .WithFilter(_coldFilterBuilder, FilterExpr)
            .WithKeyCondition(_keyConditionBuilder,
                b => b.WithPartitionKey(o => o.PK, "USER#123")
                       .WithSortKeyBeginsWith(o => o.SK, "ORDER#"));

        // Map result (cold — compiles delegate)
        var result = _resultMapper.Map(_responseAttrs, ProjectionExpr);
        return (request, result);
    }

    // --- Full pipeline warm: cached expressions + pre-compiled mapper ---

    [Benchmark]
    public (QueryRequest, OrderSummary) FullPipeline_Warm()
    {
        // Build and apply expressions via public extension methods (projection from cache)
        var request = new QueryRequest { TableName = "Orders" }
            .WithProjection(_warmProjectionBuilder, ProjectionExpr)
            .WithFilter(_warmFilterBuilder, FilterExpr)
            .WithKeyCondition(_keyConditionBuilder,
                b => b.WithPartitionKey(o => o.PK, "USER#123")
                       .WithSortKeyBeginsWith(o => o.SK, "ORDER#"));

        // Map result (warm — uses pre-compiled delegate)
        var result = _warmMapperDelegate(_responseAttrs);
        return (request, result);
    }
}
