using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using System.Linq.Expressions;
using VerifyXunit;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Snapshot tests for ConditionExpressionBuilder output (PR-05.7).
/// Locks down exact ConditionExpression, ExpressionAttributeNames, and ExpressionAttributeValues
/// for representative condition inputs: attribute_not_exists and compound conditions.
/// </summary>
public class ConditionSnapshotTests
{
    private readonly ConditionExpressionBuilder<SnapshotTestEntity> _builder;

    public ConditionSnapshotTests()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        var registry = AttributeValueConverterRegistry.Default;
        _builder = new ConditionExpressionBuilder<SnapshotTestEntity>(factory, registry);
    }

    [Fact]
    public Task ItemNotExists()
        => Verify(Build(x => DynamoDbFunctions.AttributeNotExists(x.Id)));

    [Fact]
    public Task CompoundCondition()
        => Verify(Build(x => x.StatusEnum == SnapshotStatus.Active && x.Count > 0));

    private ConditionExpressionResult Build(Expression<Func<SnapshotTestEntity, bool>> predicate)
        => _builder.BuildCondition(predicate);

    private static Task Verify(ConditionExpressionResult result)
        => Verifier.Verify(new
        {
            result.Expression,
            result.ExpressionAttributeNames,
            result.ExpressionAttributeValues
        });
}
