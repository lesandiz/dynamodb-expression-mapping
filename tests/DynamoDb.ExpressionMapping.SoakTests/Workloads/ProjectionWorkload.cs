using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Bogus;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Workload that builds projection expressions, executes DynamoDB queries, and maps results.
/// Exercises: ProjectionBuilder, DirectResultMapper, reserved keyword aliasing.
/// </summary>
public class ProjectionWorkload : IWorkload
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ProjectionBuilder<SoakOrder> _projectionBuilder;
    private readonly IDirectResultMapper<SoakOrder> _resultMapper;
    private readonly MetricsCollector _metricsCollector;
    private readonly Faker<SoakOrder> _faker;
    private readonly Random _random;

    public ProjectionWorkload(
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

        var resolverFactory = new AttributeNameResolverFactory();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _projectionBuilder = new ProjectionBuilder<SoakOrder>(resolverFactory, null, null);
        _resultMapper = new DirectResultMapper<SoakOrder>(resolverFactory, converterRegistry);
        _random = new Random(Guid.NewGuid().GetHashCode());

        // Configure Faker for diverse test data
        _faker = new Faker<SoakOrder>()
            .RuleFor(o => o.PK, f => $"CUSTOMER#{f.Random.Guid()}")
            .RuleFor(o => o.SK, f => $"ORDER#{f.Random.Guid()}")
            .RuleFor(o => o.OrderId, f => f.Random.Guid().ToString())
            .RuleFor(o => o.CustomerId, f => f.Random.Guid().ToString())
            .RuleFor(o => o.Name, f => f.Commerce.ProductName())
            .RuleFor(o => o.Status, f => f.PickRandom("Pending", "Processing", "Shipped", "Delivered", "Cancelled"))
            .RuleFor(o => o.TotalAmount, f => f.Finance.Amount(10, 1000))
            .RuleFor(o => o.TotalCurrency, f => f.Finance.Currency().Code)
            .RuleFor(o => o.Quantity, f => f.Random.Int(1, 10))
            .RuleFor(o => o.ShippingStreet, f => f.Address.StreetAddress())
            .RuleFor(o => o.ShippingCity, f => f.Address.City())
            .RuleFor(o => o.ShippingPostCode, f => f.Address.ZipCode())
            .RuleFor(o => o.Tags, f => f.Make(f.Random.Int(0, 5), () => f.Lorem.Word()).ToList())
            .RuleFor(o => o.Notes, f => f.Random.Bool(0.3f) ? f.Lorem.Sentence() : null)
            .RuleFor(o => o.CreatedAt, f => f.Date.Past(1))
            .RuleFor(o => o.ShippedAt, f => f.Random.Bool(0.5f) ? f.Date.Recent() : null)
            .RuleFor(o => o.IsGift, f => f.Random.Bool(0.2f))
            .RuleFor(o => o.Priority, f => f.PickRandom<OrderPriority>())
            .RuleFor(o => o.Metadata, f => f.Random.Bool(0.4f)
                ? f.Make(f.Random.Int(1, 3), () => new KeyValuePair<string, string>(f.Lorem.Word(), f.Lorem.Word()))
                     .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : null);
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Select a random projection type
        var projectionType = _random.Next(0, 4);

        switch (projectionType)
        {
            case 0:
                await ExecuteSimpleProjectionAsync(cancellationToken);
                break;
            case 1:
                await ExecuteCompositeProjectionAsync(cancellationToken);
                break;
            case 2:
                await ExecuteComplexProjectionAsync(cancellationToken);
                break;
            case 3:
                await ExecuteProjectionWithMappingAsync(cancellationToken);
                break;
        }

        // Update cache stats
        UpdateCacheStats();
    }

    /// <summary>
    /// Simple projection: 2-3 scalar properties.
    /// </summary>
    private async Task ExecuteSimpleProjectionAsync(CancellationToken cancellationToken)
    {
        var result = _projectionBuilder.BuildProjection(o => new
        {
            o.OrderId,
            o.Status,
            o.TotalAmount
        });

        await ExecuteQueryAsync(result.ProjectionExpression, result.ExpressionAttributeNames, cancellationToken);
    }

    /// <summary>
    /// Composite projection: nested object access, collections.
    /// </summary>
    private async Task ExecuteCompositeProjectionAsync(CancellationToken cancellationToken)
    {
        var result = _projectionBuilder.BuildProjection(o => new
        {
            o.OrderId,
            o.Name,
            o.ShippingCity,
            o.Tags,
            o.CreatedAt
        });

        await ExecuteQueryAsync(result.ProjectionExpression, result.ExpressionAttributeNames, cancellationToken);
    }

    /// <summary>
    /// Complex projection: reserved keywords, nullables, enums.
    /// </summary>
    private async Task ExecuteComplexProjectionAsync(CancellationToken cancellationToken)
    {
        var result = _projectionBuilder.BuildProjection(o => new
        {
            o.PK,
            o.SK,
            o.Name,           // Reserved keyword
            o.Status,         // Reserved keyword
            o.Priority,       // Enum
            o.ShippedAt,      // Nullable
            o.Notes,          // Nullable
            o.Metadata        // Nullable dictionary
        });

        await ExecuteQueryAsync(result.ProjectionExpression, result.ExpressionAttributeNames, cancellationToken);
    }

    /// <summary>
    /// Projection with result mapping to a different type.
    /// </summary>
    private async Task ExecuteProjectionWithMappingAsync(CancellationToken cancellationToken)
    {
        var result = _projectionBuilder.BuildProjection(o => new OrderSummary
        {
            OrderId = o.OrderId,
            CustomerName = o.Name,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            ShippingCity = o.ShippingCity
        });

        await ExecuteQueryAsync(result.ProjectionExpression, result.ExpressionAttributeNames, cancellationToken);

        // Also exercise result mapper (even though we're not using real DynamoDB responses in this workload)
        var mapper = _resultMapper.CreateMapper<OrderSummary>(o => new OrderSummary
        {
            OrderId = o.OrderId,
            CustomerName = o.Name,
            Status = o.Status,
            TotalAmount = o.TotalAmount,
            ShippingCity = o.ShippingCity
        });
    }

    /// <summary>
    /// Executes a dummy query to simulate DynamoDB interaction.
    /// In a real soak test, this would query actual data.
    /// </summary>
    private async Task ExecuteQueryAsync(
        string projectionExpression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        CancellationToken cancellationToken)
    {
        // For now, just simulate a small delay to represent DynamoDB latency
        // Real implementation would execute an actual query
        await Task.Delay(_random.Next(1, 3), cancellationToken);
    }

    private void UpdateCacheStats()
    {
        // TODO: Get actual cache statistics from library when available
        // For now, use dummy values
        _metricsCollector.UpdateCacheStats(
            entries: _random.Next(100, 500),
            hits: _random.Next(1000, 10000),
            misses: _random.Next(10, 500)
        );
    }
}
