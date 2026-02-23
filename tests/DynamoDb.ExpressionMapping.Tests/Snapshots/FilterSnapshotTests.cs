using System.Linq.Expressions;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using VerifyXunit;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Snapshot tests for FilterExpressionBuilder output (PR-05.4).
/// Locks down exact FilterExpression, ExpressionAttributeNames, and ExpressionAttributeValues
/// for representative filter inputs: equality, compound, null checks, string functions,
/// enum comparison, nested properties, and composed And/Or.
/// </summary>
public class FilterSnapshotTests
{
    private readonly FilterExpressionBuilder<SnapshotTestEntity> _builder;

    public FilterSnapshotTests()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        var registry = AttributeValueConverterRegistry.Default;
        _builder = new FilterExpressionBuilder<SnapshotTestEntity>(factory, registry);
    }

    [Fact]
    public Task SimpleEquality()
        => Verify(Build(x => x.Name == "Alice"));

    [Fact]
    public Task CompoundAndOr()
        => Verify(Build(x => (x.Name == "Alice" && x.Count > 5) || x.Enabled));

    [Fact]
    public Task NullCheck()
        => Verify(Build(x => x.OptionalScore == null));

    [Fact]
    public Task StringFunctions()
        => Verify(Build(x => x.Name.StartsWith("A") && x.Name.Contains("li")));

    [Fact]
    public Task EnumComparison()
        => Verify(Build(x => x.StatusEnum == SnapshotStatus.Active));

    [Fact]
    public Task NestedProperty()
        => Verify(Build(x => x.Address.City == "London"));

    [Fact]
    public Task ComposedAnd()
    {
        var left = Build(x => x.Name == "Alice");
        var right = Build(x => x.Count > 5);
        return Verify(FilterExpressionResult.And(left, right));
    }

    [Fact]
    public Task ComposedOr()
    {
        var left = Build(x => x.Enabled);
        var right = Build(x => x.StatusEnum == SnapshotStatus.Active);
        return Verify(FilterExpressionResult.Or(left, right));
    }

    private FilterExpressionResult Build(Expression<Func<SnapshotTestEntity, bool>> predicate)
        => _builder.BuildFilter(predicate);

    private static Task Verify(FilterExpressionResult result)
        => Verifier.Verify(new
        {
            result.Expression,
            result.ExpressionAttributeNames,
            result.ExpressionAttributeValues
        });
}
