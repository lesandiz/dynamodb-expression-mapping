using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
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
    private readonly SharedDependencies _sharedDependencies;
    private readonly Faker _faker;
    private readonly Random _random;

    public KeyConditionWorkload(
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
        _keyConditionBuilder = new KeyConditionExpressionBuilder<SoakOrder>(sharedDependencies.ResolverFactory, sharedDependencies.ConverterRegistry);
        _faker = new Faker();
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationType = _random.Next(0, 5);

        switch (operationType)
        {
            case 0:
                await BuildPartitionKeyOnly(cancellationToken);
                break;
            case 1:
                await BuildWithSortKeyEquals(cancellationToken);
                break;
            case 2:
                await BuildWithSortKeyComparison(cancellationToken);
                break;
            case 3:
                await BuildWithSortKeyBetween(cancellationToken);
                break;
            case 4:
                await BuildWithSortKeyBeginsWith(cancellationToken);
                break;
        }

        UpdateCacheStats();
    }

    /// <summary>
    /// Partition key only (no sort key condition).
    /// </summary>
    private async Task BuildPartitionKeyOnly(CancellationToken cancellationToken)
    {
        // Use seeded customer IDs (CUST0001-CUST0100)
        var customerNum = _random.Next(1, 101);
        var customerId = $"CUSTOMER#CUST{customerNum:D4}";
        var result = _keyConditionBuilder.WithPartitionKey(o => o.PK, customerId).Build();

        await ExecuteQueryAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Partition key + sort key equality.
    /// </summary>
    private async Task BuildWithSortKeyEquals(CancellationToken cancellationToken)
    {
        // Deterministic: 10 orders per customer (order 0-9 -> CUST0001, 10-19 -> CUST0002, etc.)
        var orderNum = _random.Next(0, 1000);
        var customerNum = (orderNum / 10) + 1;
        var customerId = $"CUSTOMER#CUST{customerNum:D4}";
        var orderId = $"ORDER#ORD{orderNum:D6}";

        var result = _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyEquals(o => o.SK, orderId);

        await ExecuteQueryAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Partition key + sort key comparison (GreaterThan).
    /// </summary>
    private async Task BuildWithSortKeyComparison(CancellationToken cancellationToken)
    {
        // Deterministic: pick customer, then query orders >= random start within that customer's range
        var customerNum = _random.Next(1, 101);
        var customerId = $"CUSTOMER#CUST{customerNum:D4}";

        // Pick a start order within this customer's 10 orders
        var baseOrderNum = (customerNum - 1) * 10;
        var offsetWithinCustomer = _random.Next(0, 10);
        var startOrderNum = baseOrderNum + offsetWithinCustomer;
        var orderIdPrefix = $"ORDER#ORD{startOrderNum:D6}";

        var result = _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyGreaterThan(o => o.SK, orderIdPrefix);

        await ExecuteQueryAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Partition key + sort key BETWEEN.
    /// </summary>
    private async Task BuildWithSortKeyBetween(CancellationToken cancellationToken)
    {
        // Deterministic: pick customer, then query a range within that customer's 10 orders
        var customerNum = _random.Next(1, 101);
        var customerId = $"CUSTOMER#CUST{customerNum:D4}";

        // Pick a range within this customer's 10 orders
        var baseOrderNum = (customerNum - 1) * 10;
        var startOffset = _random.Next(0, 8); // Leave room for range
        var rangeSize = _random.Next(1, 10 - startOffset); // At least 1, at most to end of range
        var startNum = baseOrderNum + startOffset;
        var endNum = startNum + rangeSize;
        var startKey = $"ORDER#ORD{startNum:D6}";
        var endKey = $"ORDER#ORD{endNum:D6}";

        var result = _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyBetween(o => o.SK, startKey, endKey);

        await ExecuteQueryAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Partition key + sort key BEGINS_WITH.
    /// </summary>
    private async Task BuildWithSortKeyBeginsWith(CancellationToken cancellationToken)
    {
        // Use seeded customer IDs (CUST0001-CUST0100)
        var customerNum = _random.Next(1, 101);
        var customerId = $"CUSTOMER#CUST{customerNum:D4}";
        var orderPrefix = "ORDER#ORD";

        var result = _keyConditionBuilder
            .WithPartitionKey(o => o.PK, customerId)
            .WithSortKeyBeginsWith(o => o.SK, orderPrefix);

        await ExecuteQueryAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    private async Task ExecuteQueryAsync(
        string keyConditionExpression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        IReadOnlyDictionary<string, AttributeValue> expressionAttributeValues,
        CancellationToken cancellationToken)
    {
        var request = new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = keyConditionExpression,
            ExpressionAttributeNames = expressionAttributeNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ExpressionAttributeValues = expressionAttributeValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Limit = 10
        };

        var response = await _dynamoDb.QueryAsync(request, cancellationToken);

        // Verify response received
        if (response == null)
        {
            throw new InvalidOperationException("DynamoDB Query returned null response");
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
