using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using VerifyXunit;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Snapshot tests for UpdateExpressionBuilder output (PR-05.5).
/// Locks down exact UpdateExpression, ExpressionAttributeNames, and ExpressionAttributeValues
/// for representative update inputs: single SET, mixed SET/REMOVE, increment/append,
/// SetIfNotExists, and all four clause types combined.
/// </summary>
public class UpdateSnapshotTests
{
    private readonly UpdateExpressionBuilder<SnapshotTestEntity> _builder;

    public UpdateSnapshotTests()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        var registry = AttributeValueConverterRegistry.Default;
        _builder = new UpdateExpressionBuilder<SnapshotTestEntity>(factory, registry);
    }

    [Fact]
    public Task SingleSet()
        => Verify(_builder
            .Set(x => x.Name, "Bob")
            .Build());

    [Fact]
    public Task MultipleSetAndRemove()
        => Verify(_builder
            .Set(x => x.Name, "Bob")
            .Set(x => x.Count, 42)
            .Remove(x => x.OptionalScore)
            .Build());

    [Fact]
    public Task IncrementAndAppend()
        => Verify(_builder
            .Increment(x => x.Count, 1)
            .AppendToList(x => x.TagList, new List<string> { "new" })
            .Build());

    [Fact]
    public Task SetIfNotExists()
        => Verify(_builder
            .SetIfNotExists(x => x.Name, "default")
            .Build());

    [Fact]
    public Task AllClauseTypes()
        => Verify(_builder
            .Set(x => x.Name, "Updated")
            .Remove(x => x.OptionalScore)
            .Add(x => x.Count, 1)
            .Delete(x => x.Categories, new HashSet<string> { "old" })
            .Build());

    private static Task Verify(UpdateExpressionResult result)
        => Verifier.Verify(new
        {
            result.Expression,
            result.ExpressionAttributeNames,
            result.ExpressionAttributeValues
        });
}
