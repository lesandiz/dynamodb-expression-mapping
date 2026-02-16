using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
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
    private readonly SharedDependencies _sharedDependencies;
    private readonly Faker _faker;
    private readonly Random _random;

    public FilterWorkload(
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector,
        SharedDependencies sharedDependencies)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));
        ArgumentNullException.ThrowIfNull(sharedDependencies);

        _sharedDependencies = sharedDependencies;
        _filterBuilder = new FilterExpressionBuilder<SoakOrder>(sharedDependencies.ResolverFactory, sharedDependencies.ConverterRegistry);
        _faker = new Faker();
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationType = _random.Next(0, 5);

        switch (operationType)
        {
            case 0:
                await BuildSimpleFilter();
                break;
            case 1:
                await BuildCompositeFilter();
                break;
            case 2:
                await BuildComplexFilter();
                break;
            case 3:
                await BuildAndComposeFilters();
                break;
            case 4:
                await BuildOrComposeFilters();
                break;
        }

        UpdateCacheStats();
    }

    /// <summary>
    /// Simple filter: single comparison on a scalar property.
    /// </summary>
    private async Task BuildSimpleFilter()
    {
        var status = _faker.PickRandom("Pending", "Processing", "Shipped", "Delivered");
        var result = _filterBuilder.BuildFilter(o => o.Status == status);

        await ExecuteScanAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues);
    }

    /// <summary>
    /// Composite filter: multiple conditions combined with AND.
    /// </summary>
    private async Task BuildCompositeFilter()
    {
        var minAmount = _faker.Random.Decimal(10, 100);
        var maxAmount = minAmount + _faker.Random.Decimal(100, 500);

        var result = _filterBuilder.BuildFilter(o =>
            o.Status == "Processing" &&
            o.TotalAmount >= minAmount &&
            o.TotalAmount <= maxAmount);

        await ExecuteScanAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues);
    }

    /// <summary>
    /// Complex filter: OR conditions, nullability checks, string functions, enum comparisons.
    /// </summary>
    private async Task BuildComplexFilter()
    {
        var city = _faker.Address.City();

        var result = _filterBuilder.BuildFilter(o =>
            (o.Status == "Shipped" || o.Status == "Delivered") &&
            o.ShippingCity == city &&
            o.ShippedAt != null &&
            o.Priority == OrderPriority.High);

        await ExecuteScanAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues);
    }

    /// <summary>
    /// Build two independent filters and compose with And().
    /// Tests re-aliasing to prevent collisions.
    /// </summary>
    private async Task BuildAndComposeFilters()
    {
        var filter1 = _filterBuilder.BuildFilter(o => o.Status == "Processing");
        var filter2 = _filterBuilder.BuildFilter(o => o.TotalAmount > 100);

        var composed = FilterExpressionResult.And(filter1, filter2);

        // Verify no alias collisions (basic check)
        if (string.IsNullOrEmpty(composed.Expression) || composed.ExpressionAttributeValues.Count == 0)
        {
            throw new InvalidOperationException("Filter composition produced invalid result");
        }

        await ExecuteScanAsync(composed.Expression, composed.ExpressionAttributeNames, composed.ExpressionAttributeValues);
    }

    /// <summary>
    /// Build two independent filters and compose with Or().
    /// </summary>
    private async Task BuildOrComposeFilters()
    {
        var priority = _faker.PickRandom<OrderPriority>();
        var isGift = _faker.Random.Bool();

        var filter1 = _filterBuilder.BuildFilter(o => o.Priority == priority);
        var filter2 = _filterBuilder.BuildFilter(o => o.IsGift == isGift);

        var composed = FilterExpressionResult.Or(filter1, filter2);

        if (string.IsNullOrEmpty(composed.Expression) || composed.ExpressionAttributeValues.Count == 0)
        {
            throw new InvalidOperationException("Filter composition produced invalid result");
        }

        await ExecuteScanAsync(composed.Expression, composed.ExpressionAttributeNames, composed.ExpressionAttributeValues);
    }

    private async Task ExecuteScanAsync(
        string filterExpression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        IReadOnlyDictionary<string, AttributeValue> expressionAttributeValues,
        CancellationToken cancellationToken = default)
    {
        var request = new ScanRequest
        {
            TableName = _tableName,
            FilterExpression = filterExpression,
            ExpressionAttributeNames = expressionAttributeNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ExpressionAttributeValues = expressionAttributeValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Limit = 10
        };

        var response = await _dynamoDb.ScanAsync(request, cancellationToken);

        // Verify response received
        if (response == null)
        {
            throw new InvalidOperationException("DynamoDB Scan returned null response");
        }
    }

    private void UpdateCacheStats()
    {
        var stats = _sharedDependencies.GetCacheStatistics();
        var totalHits = stats.ProjectionHits + stats.MapperHits + stats.FilterHits;
        var totalMisses = stats.ProjectionMisses + stats.MapperMisses + stats.FilterMisses;

        _metricsCollector.UpdateCacheStats(
            entries: stats.TotalEntries,
            hits: totalHits,
            misses: totalMisses
        );
    }
}
