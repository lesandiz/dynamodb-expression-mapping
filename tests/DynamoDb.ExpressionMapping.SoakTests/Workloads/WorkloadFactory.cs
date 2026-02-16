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
    /// All workloads share the same dependencies to test thread-safety (PR-02.2).
    /// </summary>
    public static IWorkload CreateWorkload(
        string workloadType,
        IAmazonDynamoDB dynamoDb,
        string tableName,
        MetricsCollector metricsCollector,
        SharedDependencies sharedDependencies)
    {
        return workloadType.ToLowerInvariant() switch
        {
            "projection" => new ProjectionWorkload(dynamoDb, tableName, metricsCollector, sharedDependencies),
            "filter" => new FilterWorkload(dynamoDb, tableName, metricsCollector, sharedDependencies),
            "update" => new UpdateWorkload(dynamoDb, tableName, metricsCollector, sharedDependencies),
            "key-condition" or "keycondition" => new KeyConditionWorkload(dynamoDb, tableName, metricsCollector, sharedDependencies),
            "cache-stress" or "cachestress" => new CacheStressWorkload(dynamoDb, tableName, metricsCollector, sharedDependencies),
            "mixed" or _ => new MixedWorkload(dynamoDb, tableName, metricsCollector, sharedDependencies)
        };
    }
}
