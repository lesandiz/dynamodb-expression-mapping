using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using VerifyXunit;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Smoke test to verify Verify configuration and AttributeValue converter work correctly.
/// </summary>
public class VerifyConfigSmokeTest
{
    [Fact]
    public Task ProjectionResult_Serializes()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        var builder = new ProjectionBuilder<TestEntity>(
            factory, ReservedKeywordRegistry.Default, NullExpressionCache.Instance);

        var result = builder.BuildProjection(p => new { p.OrderId, p.Name });

        return Verifier.Verify(new
        {
            result.ProjectionExpression,
            result.ExpressionAttributeNames,
            result.Shape
        });
    }

    [Fact]
    public Task FilterResult_Serializes()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        var registry = AttributeValueConverterRegistry.Default;
        var builder = new FilterExpressionBuilder<FilterTestEntity>(factory, registry);

        var result = builder.BuildFilter(p => p.OrderId == "12345");

        return Verifier.Verify(new
        {
            result.Expression,
            result.ExpressionAttributeNames,
            result.ExpressionAttributeValues
        });
    }
}
