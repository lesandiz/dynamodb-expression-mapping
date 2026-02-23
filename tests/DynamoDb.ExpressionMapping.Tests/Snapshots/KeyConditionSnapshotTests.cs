using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using VerifyXunit;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Snapshot tests for KeyConditionExpressionBuilder output (PR-05.6).
/// Locks down exact KeyConditionExpression, ExpressionAttributeNames, and ExpressionAttributeValues
/// for representative key condition inputs: partition-only, PK+SK equality, between, begins_with,
/// and reserved keyword attributes.
/// </summary>
public class KeyConditionSnapshotTests
{
    private readonly KeyConditionExpressionBuilder<SnapshotTestEntity> _builder;

    public KeyConditionSnapshotTests()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        var registry = AttributeValueConverterRegistry.Default;
        _builder = new KeyConditionExpressionBuilder<SnapshotTestEntity>(factory, registry);
    }

    [Fact]
    public Task PartitionKeyOnly()
        => Verify(_builder
            .WithPartitionKey(x => x.PK, "USER#123")
            .Build());

    [Fact]
    public Task PartitionKeyAndSortKeyEquals()
        => Verify(_builder
            .WithPartitionKey(x => x.PK, "USER#123")
            .WithSortKeyEquals(x => x.SK, "ORDER#456"));

    [Fact]
    public Task PartitionKeyAndSortKeyBetween()
        => Verify(_builder
            .WithPartitionKey(x => x.PK, "USER#123")
            .WithSortKeyBetween(x => x.SK, "ORDER#100", "ORDER#999"));

    [Fact]
    public Task PartitionKeyAndSortKeyBeginsWith()
        => Verify(_builder
            .WithPartitionKey(x => x.PK, "USER#123")
            .WithSortKeyBeginsWith(x => x.SK, "ORDER#"));

    [Fact]
    public Task ReservedKeywordAttributes()
        => Verify(_builder
            .WithPartitionKey(x => x.Name, "Alice")
            .WithSortKeyEquals(x => x.Status, "Active"));

    private static Task Verify(KeyConditionExpressionResult result)
        => Verifier.Verify(new
        {
            result.Expression,
            result.ExpressionAttributeNames,
            result.ExpressionAttributeValues
        });
}
