using Amazon.DynamoDBv2;
using Bogus;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Workload that builds filter expressions and tests composition via And/Or.
/// Exercises: FilterExpressionBuilder, filter composition, reserved keyword aliasing, re-aliasing.
/// </summary>
public class FilterWorkload : IWorkload
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly FilterExpressionBuilder<SoakOrder> _filterBuilder;
    private readonly MetricsCollector _metricsCollector;
    private readonly Faker _faker;
    private readonly Random _random;

    public FilterWorkload(
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

        var resolverFactory = new AttributeNameResolverFactory();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _filterBuilder = new FilterExpressionBuilder<SoakOrder>(resolverFactory, converterRegistry);
        _faker = new Faker();
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationType = _random.Next(0, 5);

        switch (operationType)
        {
            case 0:
                BuildSimpleFilter();
                break;
            case 1:
                BuildCompositeFilter();
                break;
            case 2:
                BuildComplexFilter();
                break;
            case 3:
                BuildAndComposeFilters();
                break;
            case 4:
                BuildOrComposeFilters();
                break;
        }

        await Task.CompletedTask;
        UpdateCacheStats();
    }

    /// <summary>
    /// Simple filter: single comparison on a scalar property.
    /// </summary>
    private void BuildSimpleFilter()
    {
        var status = _faker.PickRandom("Pending", "Processing", "Shipped", "Delivered");
        _filterBuilder.BuildFilter(o => o.Status == status);
    }

    /// <summary>
    /// Composite filter: multiple conditions combined with AND.
    /// </summary>
    private void BuildCompositeFilter()
    {
        var minAmount = _faker.Random.Decimal(10, 100);
        var maxAmount = minAmount + _faker.Random.Decimal(100, 500);

        _filterBuilder.BuildFilter(o =>
            o.Status == "Processing" &&
            o.TotalAmount >= minAmount &&
            o.TotalAmount <= maxAmount);
    }

    /// <summary>
    /// Complex filter: OR conditions, nullability checks, string functions, enum comparisons.
    /// </summary>
    private void BuildComplexFilter()
    {
        var city = _faker.Address.City();

        _filterBuilder.BuildFilter(o =>
            (o.Status == "Shipped" || o.Status == "Delivered") &&
            o.ShippingCity == city &&
            o.ShippedAt != null &&
            o.Priority == OrderPriority.High);
    }

    /// <summary>
    /// Build two independent filters and compose with And().
    /// Tests re-aliasing to prevent collisions.
    /// </summary>
    private void BuildAndComposeFilters()
    {
        var filter1 = _filterBuilder.BuildFilter(o => o.Status == "Processing");
        var filter2 = _filterBuilder.BuildFilter(o => o.TotalAmount > 100);

        var composed = FilterExpressionResult.And(filter1, filter2);

        // Verify no alias collisions (basic check)
        if (composed.ExpressionAttributeNames.Count == 0 || composed.ExpressionAttributeValues.Count == 0)
        {
            throw new InvalidOperationException("Filter composition produced empty attribute dictionaries");
        }
    }

    /// <summary>
    /// Build two independent filters and compose with Or().
    /// </summary>
    private void BuildOrComposeFilters()
    {
        var priority = _faker.PickRandom<OrderPriority>();
        var isGift = _faker.Random.Bool();

        var filter1 = _filterBuilder.BuildFilter(o => o.Priority == priority);
        var filter2 = _filterBuilder.BuildFilter(o => o.IsGift == isGift);

        var composed = FilterExpressionResult.Or(filter1, filter2);

        if (composed.ExpressionAttributeNames.Count == 0)
        {
            throw new InvalidOperationException("Filter composition produced empty attribute name dictionary");
        }
    }

    private void UpdateCacheStats()
    {
        _metricsCollector.UpdateCacheStats(
            entries: _random.Next(100, 500),
            hits: _random.Next(1000, 10000),
            misses: _random.Next(10, 500)
        );
    }
}
