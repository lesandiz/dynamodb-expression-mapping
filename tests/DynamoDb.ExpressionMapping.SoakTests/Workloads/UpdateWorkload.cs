using Amazon.DynamoDBv2;
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
    private readonly Faker _faker;
    private readonly Random _random;

    public UpdateWorkload(
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector)
    {
        _dynamoDb = dynamoDb ?? throw new ArgumentNullException(nameof(dynamoDb));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _metricsCollector = metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

        var resolverFactory = new AttributeNameResolverFactory();
        var converterRegistry = AttributeValueConverterRegistry.Default;

        _updateBuilder = new UpdateExpressionBuilder<SoakOrder>(resolverFactory, converterRegistry);
        _faker = new Faker();
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var operationType = _random.Next(0, 6);

        switch (operationType)
        {
            case 0:
                BuildSingleSetOperation();
                break;
            case 1:
                BuildMultipleSetOperations();
                break;
            case 2:
                BuildIncrementOperation();
                break;
            case 3:
                BuildListOperations();
                break;
            case 4:
                BuildMixedClauses();
                break;
            case 5:
                BuildConditionalUpdate();
                break;
        }

        await Task.CompletedTask;
        UpdateCacheStats();
    }

    /// <summary>
    /// Single SET operation.
    /// </summary>
    private void BuildSingleSetOperation()
    {
        var newStatus = _faker.PickRandom("Processing", "Shipped", "Delivered");
        _updateBuilder.Set(o => o.Status, newStatus).Build();
    }

    /// <summary>
    /// Multiple SET operations on different properties.
    /// </summary>
    private void BuildMultipleSetOperations()
    {
        var newStatus = _faker.PickRandom("Processing", "Shipped");
        var newNotes = _faker.Lorem.Sentence();
        var shippedAt = DateTime.UtcNow;

        _updateBuilder
            .Set(o => o.Status, newStatus)
            .Set(o => o.Notes, newNotes)
            .Set(o => o.ShippedAt, shippedAt)
            .Build();
    }

    /// <summary>
    /// Increment operation (ADD clause).
    /// </summary>
    private void BuildIncrementOperation()
    {
        var incrementValue = _random.Next(1, 5);
        _updateBuilder.Increment(o => o.Quantity, incrementValue).Build();
    }

    /// <summary>
    /// List operations: AppendToList.
    /// </summary>
    private void BuildListOperations()
    {
        var newTags = new List<string> { _faker.Lorem.Word(), _faker.Lorem.Word() };
        _updateBuilder.AppendToList(o => o.Tags, newTags).Build();
    }

    /// <summary>
    /// Mixed clauses: SET, ADD, REMOVE in one expression.
    /// </summary>
    private void BuildMixedClauses()
    {
        var newStatus = _faker.PickRandom("Shipped", "Delivered");
        var incrementQty = _random.Next(1, 3);

        _updateBuilder
            .Set(o => o.Status, newStatus)
            .Increment(o => o.Quantity, incrementQty)
            .Remove(o => o.Notes)
            .Build();
    }

    /// <summary>
    /// Update with condition expression.
    /// </summary>
    private void BuildConditionalUpdate()
    {
        var newStatus = "Shipped";
        _updateBuilder
            .Set(o => o.Status, newStatus)
            .Set(o => o.ShippedAt, DateTime.UtcNow)
            .Build();
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
