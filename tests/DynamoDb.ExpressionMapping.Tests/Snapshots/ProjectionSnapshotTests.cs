using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using VerifyXunit;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Snapshot tests for ProjectionBuilder output (PR-05.3).
/// Locks down exact ProjectionExpression, ExpressionAttributeNames, and Shape
/// for representative inputs across single, nested, reserved, and remapped properties.
/// </summary>
public class ProjectionSnapshotTests
{
    private readonly ProjectionBuilder<SnapshotTestEntity> _builder;

    public ProjectionSnapshotTests()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        _builder = new ProjectionBuilder<SnapshotTestEntity>(
            factory, ReservedKeywordRegistry.Default, NullExpressionCache.Instance);
    }

    [Fact]
    public Task SingleProperty()
        => Verify(Build(x => x.Id));

    [Fact]
    public Task MultipleProperties_AnonymousType()
        => Verify(Build(x => new { x.Id, x.Name, x.Count }));

    [Fact]
    public Task NestedProperty()
        => Verify(Build(x => new { x.Id, x.Address.City }));

    [Fact]
    public Task DeeplyNestedProperty()
        => Verify(Build(x => x.Contact.MailingAddress.PostCode));

    [Fact]
    public Task ReservedKeywords()
        => Verify(Build(x => new { x.Name, x.Status }));

    [Fact]
    public Task RemappedAttribute()
        => Verify(Build(x => x.CustomerId));

    [Fact]
    public Task MixedReservedAndRemapped()
        => Verify(Build(x => new { x.Name, x.CustomerId, x.Address.City }));

    private ProjectionResult Build<TResult>(
        System.Linq.Expressions.Expression<Func<SnapshotTestEntity, TResult>> selector)
        => _builder.BuildProjection(selector);

    private static Task Verify(ProjectionResult result)
        => Verifier.Verify(new
        {
            result.ProjectionExpression,
            result.ExpressionAttributeNames,
            result.Shape
        });
}
