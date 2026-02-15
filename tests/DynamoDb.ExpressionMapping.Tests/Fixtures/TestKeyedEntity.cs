using DynamoDb.ExpressionMapping.Attributes;

namespace DynamoDb.ExpressionMapping.Tests.Fixtures;

/// <summary>
/// Entity with explicit partition key and sort key for KeyConditionExpressionBuilder tests.
/// </summary>
public class TestKeyedEntity
{
    public string PK { get; set; } = string.Empty;                      // Partition key
    public string SK { get; set; } = string.Empty;                      // Sort key
    public string Data { get; set; } = string.Empty;
    public TestStatus Status { get; set; }              // Reserved keyword

    [DynamoDbIgnore]
    public bool IsRecent => SK?.StartsWith("2024") == true;
}
