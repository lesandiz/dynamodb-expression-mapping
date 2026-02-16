using Amazon.DynamoDBv2;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Workload that randomly selects and executes operations from all other workloads.
/// Simulates realistic production usage with varied operation mix.
/// </summary>
public class MixedWorkload : IWorkload
{
    private readonly ProjectionWorkload _projectionWorkload;
    private readonly FilterWorkload _filterWorkload;
    private readonly UpdateWorkload _updateWorkload;
    private readonly KeyConditionWorkload _keyConditionWorkload;
    private readonly Random _random;

    // Weight distribution per PR-02.4
    private const int ProjectionWeight = 30;
    private const int KeyConditionWeight = 20;
    private const int FilterWeight = 15;
    private const int UpdateWeight = 15;
    private const int FilterCompositionWeight = 10;
    private const int CacheStressWeight = 10;

    private readonly int _totalWeight =
        ProjectionWeight + KeyConditionWeight + FilterWeight +
        UpdateWeight + FilterCompositionWeight + CacheStressWeight;

    public MixedWorkload(
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector)
    {
        _projectionWorkload = new ProjectionWorkload(dynamoDb, tableName, metricsCollector);
        _filterWorkload = new FilterWorkload(dynamoDb, tableName, metricsCollector);
        _updateWorkload = new UpdateWorkload(dynamoDb, tableName, metricsCollector);
        _keyConditionWorkload = new KeyConditionWorkload(dynamoDb, tableName, metricsCollector);
        _random = new Random(Guid.NewGuid().GetHashCode());
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var roll = _random.Next(0, _totalWeight);

        if (roll < ProjectionWeight)
        {
            // 30% - Projection
            await _projectionWorkload.ExecuteAsync(cancellationToken);
        }
        else if (roll < ProjectionWeight + KeyConditionWeight)
        {
            // 20% - Key condition
            await _keyConditionWorkload.ExecuteAsync(cancellationToken);
        }
        else if (roll < ProjectionWeight + KeyConditionWeight + FilterWeight)
        {
            // 15% - Filter
            await _filterWorkload.ExecuteAsync(cancellationToken);
        }
        else if (roll < ProjectionWeight + KeyConditionWeight + FilterWeight + UpdateWeight)
        {
            // 15% - Update
            await _updateWorkload.ExecuteAsync(cancellationToken);
        }
        else if (roll < ProjectionWeight + KeyConditionWeight + FilterWeight + UpdateWeight + FilterCompositionWeight)
        {
            // 10% - Filter composition (handled by FilterWorkload)
            await _filterWorkload.ExecuteAsync(cancellationToken);
        }
        else
        {
            // 10% - Cache stress (use projection with unique selectors)
            await _projectionWorkload.ExecuteAsync(cancellationToken);
        }
    }
}
