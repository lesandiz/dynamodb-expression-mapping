using Amazon.DynamoDBv2;
using DynamoDb.ExpressionMapping.SoakTests.Metrics;

namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Factory for creating workload instances based on workload type.
/// </summary>
public static class WorkloadFactory
{
    /// <summary>
    /// Creates a workload instance based on the specified workload type.
    /// </summary>
    public static IWorkload CreateWorkload(
        string workloadType,
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector)
    {
        return workloadType.ToLowerInvariant() switch
        {
            "projection" => new ProjectionWorkload(dynamoDb, tableName, metricsCollector),
            "filter" => new FilterWorkload(dynamoDb, tableName, metricsCollector),
            "update" => new UpdateWorkload(dynamoDb, tableName, metricsCollector),
            "key-condition" or "keycondition" => new KeyConditionWorkload(dynamoDb, tableName, metricsCollector),
            "cache-stress" or "cachestress" => new CacheStressWorkload(dynamoDb, tableName, metricsCollector),
            "mixed" or _ => new MixedWorkload(dynamoDb, tableName, metricsCollector)
        };
    }
}
