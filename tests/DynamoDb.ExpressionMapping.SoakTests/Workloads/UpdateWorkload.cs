using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Bogus;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Workload that builds update expressions using the fluent API.
/// Exercises: UpdateExpressionBuilder, SET/ADD/DELETE/REMOVE clauses, orphaned placeholder cleanup.
/// </summary>
public class UpdateWorkload : IWorkload
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly UpdateExpressionBuilder<SoakOrder> _updateBuilder;
    private readonly MetricsCollector _metricsCollector;
    private readonly SharedDependencies _sharedDependencies;
    private readonly Faker _faker;
    private readonly Random _random;

    public UpdateWorkload(
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
        _updateBuilder = new UpdateExpressionBuilder<SoakOrder>(sharedDependencies.ResolverFactory, sharedDependencies.ConverterRegistry);
        _faker = new Faker();
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationType = _random.Next(0, 6);

        switch (operationType)
        {
            case 0:
                await BuildSingleSetOperation(cancellationToken);
                break;
            case 1:
                await BuildMultipleSetOperations(cancellationToken);
                break;
            case 2:
                await BuildIncrementOperation(cancellationToken);
                break;
            case 3:
                await BuildListOperations(cancellationToken);
                break;
            case 4:
                await BuildMixedClauses(cancellationToken);
                break;
            case 5:
                await BuildConditionalUpdate(cancellationToken);
                break;
        }

        UpdateCacheStats();
    }

    /// <summary>
    /// Single SET operation.
    /// </summary>
    private async Task BuildSingleSetOperation(CancellationToken cancellationToken)
    {
        var newStatus = _faker.PickRandom("Processing", "Shipped", "Delivered");
        var result = _updateBuilder.Set(o => o.Status, newStatus).Build();

        await ExecuteUpdateAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Multiple SET operations on different properties.
    /// </summary>
    private async Task BuildMultipleSetOperations(CancellationToken cancellationToken)
    {
        var newStatus = _faker.PickRandom("Processing", "Shipped");
        var newNotes = _faker.Lorem.Sentence();
        var shippedAt = DateTime.UtcNow;

        var result = _updateBuilder
            .Set(o => o.Status, newStatus)
            .Set(o => o.Notes, newNotes)
            .Set(o => o.ShippedAt, shippedAt)
            .Build();

        await ExecuteUpdateAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Increment operation (ADD clause).
    /// </summary>
    private async Task BuildIncrementOperation(CancellationToken cancellationToken)
    {
        var incrementValue = _random.Next(1, 5);
        var result = _updateBuilder.Increment(o => o.Quantity, incrementValue).Build();

        await ExecuteUpdateAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// List operations: AppendToList.
    /// </summary>
    private async Task BuildListOperations(CancellationToken cancellationToken)
    {
        var newTags = new List<string> { _faker.Lorem.Word(), _faker.Lorem.Word() };
        var result = _updateBuilder.AppendToList(o => o.Tags, newTags).Build();

        await ExecuteUpdateAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Mixed clauses: SET, ADD, REMOVE in one expression.
    /// </summary>
    private async Task BuildMixedClauses(CancellationToken cancellationToken)
    {
        var newStatus = _faker.PickRandom("Shipped", "Delivered");
        var incrementQty = _random.Next(1, 3);

        var result = _updateBuilder
            .Set(o => o.Status, newStatus)
            .Increment(o => o.Quantity, incrementQty)
            .Remove(o => o.Notes)
            .Build();

        await ExecuteUpdateAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    /// <summary>
    /// Update with condition expression.
    /// </summary>
    private async Task BuildConditionalUpdate(CancellationToken cancellationToken)
    {
        var newStatus = "Shipped";
        var result = _updateBuilder
            .Set(o => o.Status, newStatus)
            .Set(o => o.ShippedAt, DateTime.UtcNow)
            .Build();

        await ExecuteUpdateAsync(result.Expression, result.ExpressionAttributeNames, result.ExpressionAttributeValues, cancellationToken);
    }

    private async Task ExecuteUpdateAsync(
        string updateExpression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        IReadOnlyDictionary<string, AttributeValue> expressionAttributeValues,
        CancellationToken cancellationToken)
    {
        // Deterministic: 10 orders per customer (order 0-9 -> CUST0001, 10-19 -> CUST0002, etc.)
        var orderNum = _random.Next(0, 1000);
        var customerNum = (orderNum / 10) + 1;
        var customerId = $"CUSTOMER#CUST{customerNum:D4}";
        var orderId = $"ORDER#ORD{orderNum:D6}";

        var request = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = customerId },
                ["SK"] = new AttributeValue { S = orderId }
            },
            UpdateExpression = updateExpression,
            ExpressionAttributeNames = expressionAttributeNames.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ExpressionAttributeValues = expressionAttributeValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ReturnValues = ReturnValue.ALL_NEW
        };

        var response = await _dynamoDb.UpdateItemAsync(request, cancellationToken);

        // Verify response received
        if (response == null)
        {
            throw new InvalidOperationException("DynamoDB UpdateItem returned null response");
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
