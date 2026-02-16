using Amazon.DynamoDBv2;
using Bogus;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Workload that builds key condition expressions using the staged fluent API.
/// Exercises: KeyConditionExpressionBuilder, partition key equality + sort key comparisons.
/// </summary>
public class KeyConditionWorkload : IWorkload
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly KeyConditionExpressionBuilder<SoakOrder> _keyConditionBuilder;
    private readonly MetricsCollector _metricsCollector;
    private readonly Faker _faker;
    private readonly Random _random;

    public KeyConditionWorkload(
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

        var resolverFactory = new AttributeNameResolverFactory();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _keyConditionBuilder = new KeyConditionExpressionBuilder<SoakOrder>(resolverFactory, converterRegistry);
        _faker = new Faker();
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationType = _random.Next(0, 5);

        switch (operationType)
        {
            case 0:
                BuildPartitionKeyOnly();
                break;
            case 1:
                BuildWithSortKeyEquals();
                break;
            case 2:
                BuildWithSortKeyComparison();
                break;
            case 3:
                BuildWithSortKeyBetween();
                break;
            case 4:
                BuildWithSortKeyBeginsWith();
                break;
        }

        await Task.CompletedTask;
        UpdateCacheStats();
    }

    /// <summary>
    /// Partition key only (no sort key condition).
    /// </summary>
    private void BuildPartitionKeyOnly()
    {
        var customerId = $"CUSTOMER#{_faker.Random.Guid()}";
        _keyConditionBuilder.WithPartitionKey(o => o.PK, customerId).Build();
    }

    /// <summary>
    /// Partition key + sort key equality.
    /// </summary>
    private void BuildWithSortKeyEquals()
    {
        var customerId = $"CUSTOMER#{_faker.Random.Guid()}";
        var orderId = $"ORDER#{_faker.Random.Guid()}";

        _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyEquals(o => o.SK, orderId);
    }

    /// <summary>
    /// Partition key + sort key comparison (GreaterThan).
    /// </summary>
    private void BuildWithSortKeyComparison()
    {
        var customerId = $"CUSTOMER#{_faker.Random.Guid()}";
        var orderIdPrefix = $"ORDER#2024";

        _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyGreaterThan(o => o.SK, orderIdPrefix);
    }

    /// <summary>
    /// Partition key + sort key BETWEEN.
    /// </summary>
    private void BuildWithSortKeyBetween()
    {
        var customerId = $"CUSTOMER#{_faker.Random.Guid()}";
        var startKey = $"ORDER#2024-01";
        var endKey = $"ORDER#2024-12";

        _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyBetween(o => o.SK, startKey, endKey);
    }

    /// <summary>
    /// Partition key + sort key BEGINS_WITH.
    /// </summary>
    private void BuildWithSortKeyBeginsWith()
    {
        var customerId = $"CUSTOMER#{_faker.Random.Guid()}";
        var orderPrefix = "ORDER#";

        _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyBeginsWith(o => o.SK, orderPrefix);
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
