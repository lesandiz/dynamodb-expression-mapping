using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using VerifyXunit;

namespace DynamoDb.ExpressionMapping.Tests.Snapshots;

/// <summary>
/// Snapshot tests for combined expression application to SDK request types (PR-05.8).
/// Verifies that KeyCondition, Projection, and Filter expressions merge correctly
/// onto a QueryRequest with proper alias scope isolation (#key_, #proj_, #filt_ prefixes).
/// </summary>
public class CombinedExpressionSnapshotTests
{
    private readonly ProjectionBuilder<SnapshotTestEntity> _projectionBuilder;
    private readonly FilterExpressionBuilder<SnapshotTestEntity> _filterBuilder;
    private readonly KeyConditionExpressionBuilder<SnapshotTestEntity> _keyConditionBuilder;

    public CombinedExpressionSnapshotTests()
    {
        var factory = new AttributeNameResolverFactoryBuilder().Build();
        var registry = AttributeValueConverterRegistry.Default;

        _projectionBuilder = new ProjectionBuilder<SnapshotTestEntity>(
            factory, ReservedKeywordRegistry.Default, NullExpressionCache.Instance);
        _filterBuilder = new FilterExpressionBuilder<SnapshotTestEntity>(factory, registry);
        _keyConditionBuilder = new KeyConditionExpressionBuilder<SnapshotTestEntity>(factory, registry);
    }

    [Fact]
    public Task QueryRequest_KeyCondition_Projection_Filter()
    {
        var request = new QueryRequest { TableName = "TestTable" };

        request
            .WithKeyCondition(_keyConditionBuilder,
                b => b.WithPartitionKey(x => x.PK, "USER#123")
                      .WithSortKeyBeginsWith(x => x.SK, "ORDER#"))
            .WithProjection(_projectionBuilder,
                (SnapshotTestEntity x) => new { x.Id, x.Name, x.Count })
            .WithFilter(_filterBuilder,
                (SnapshotTestEntity x) => x.StatusEnum == SnapshotStatus.Active && x.Count > 0);

        return Verifier.Verify(new
        {
            request.KeyConditionExpression,
            request.ProjectionExpression,
            request.FilterExpression,
            request.ExpressionAttributeNames,
            request.ExpressionAttributeValues
        });
    }
}
