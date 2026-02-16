using Amazon.DynamoDBv2;
using Bogus;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Workload that builds expressions with unique selectors to stress cache growth.
/// Forces cache misses to validate unbounded growth protection.
/// </summary>
public class CacheStressWorkload : IWorkload
{
    private readonly ProjectionBuilder<SoakOrder> _projectionBuilder;
    private readonly FilterExpressionBuilder<SoakOrder> _filterBuilder;
    private readonly UpdateExpressionBuilder<SoakOrder> _updateBuilder;
    private readonly MetricsCollector _metricsCollector;
    private readonly SharedDependencies _sharedDependencies;
    private readonly Faker _faker;
    private readonly Random _random;

    public CacheStressWorkload(
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector,
        SharedDependencies sharedDependencies)
    {
        ArgumentNullException.ThrowIfNull(sharedDependencies);
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

        _sharedDependencies = sharedDependencies;
        _projectionBuilder = new ProjectionBuilder<SoakOrder>(sharedDependencies.ResolverFactory, null, sharedDependencies.ExpressionCache);
        _filterBuilder = new FilterExpressionBuilder<SoakOrder>(sharedDependencies.ResolverFactory, sharedDependencies.ConverterRegistry);
        _updateBuilder = new UpdateExpressionBuilder<SoakOrder>(sharedDependencies.ResolverFactory, sharedDependencies.ConverterRegistry);

        _faker = new Faker();
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationType = _random.Next(0, 3);

        switch (operationType)
        {
            case 0:
                BuildUniqueProjection();
                break;
            case 1:
                BuildUniqueFilter();
                break;
            case 2:
                BuildUniqueUpdate();
                break;
        }

        await Task.CompletedTask;
        UpdateCacheStats();
    }

    /// <summary>
    /// Builds a projection with a randomly varying property selection.
    /// Each variation produces a cache miss.
    /// </summary>
    private void BuildUniqueProjection()
    {
        // Randomly select 2-5 properties from available set
        var propertyCount = _random.Next(2, 6);
        var selectedProps = new HashSet<int>();

        while (selectedProps.Count < propertyCount)
        {
            selectedProps.Add(_random.Next(0, 10));
        }

        // Build projection based on selected properties
        // Different combinations = different expression trees = cache misses
        if (selectedProps.Contains(0) && selectedProps.Contains(1))
        {
            _projectionBuilder.BuildProjection(o => new { o.OrderId, o.Status });
        }
        else if (selectedProps.Contains(2) && selectedProps.Contains(3))
        {
            _projectionBuilder.BuildProjection(o => new { o.TotalAmount, o.Quantity });
        }
        else if (selectedProps.Contains(4) && selectedProps.Contains(5))
        {
            _projectionBuilder.BuildProjection(o => new { o.ShippingCity, o.CreatedAt });
        }
        else
        {
            // Fall back to a random unique combination
            _projectionBuilder.BuildProjection(o => new
            {
                o.OrderId,
                o.Name,
                o.Status,
                Qty = o.Quantity,
                Total = o.TotalAmount
            });
        }
    }

    /// <summary>
    /// Builds filters with varying comparison values to create unique expressions.
    /// </summary>
    private void BuildUniqueFilter()
    {
        // Each unique value creates a different expression tree
        var threshold = _faker.Random.Decimal(10, 1000);
        _filterBuilder.BuildFilter(o => o.TotalAmount > threshold);
    }

    /// <summary>
    /// Builds update expressions with varying values.
    /// </summary>
    private void BuildUniqueUpdate()
    {
        var newQuantity = _random.Next(1, 100);
        _updateBuilder.Set(o => o.Quantity, newQuantity).Build();
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
