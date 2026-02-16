namespace DynamoDb.ExpressionMapping.SoakTests.Workloads;

/// <summary>
/// Represents a workload that can be executed during soak testing.
/// Each workload exercises specific library subsystems.
/// </summary>
public interface IWorkload
{
    /// <summary>
    /// Executes one iteration of the workload.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
