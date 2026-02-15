using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;
using NSubstitute;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for combined extension usage (Spec 10 §7, §9).
/// </summary>
public class CombinedExtensionsTests
{
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public void QueryRequest_WithExpressions_AppliesProjectionAndFilter()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };

        var projectionBuilder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, object>> selector = o => new { o.OrderId, o.Status };

        var projectionResult = new ProjectionResult(
            "OrderId, #proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "Status" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.Composite,
            new[] { "OrderId", "Status" });

        projectionBuilder.BuildProjection(selector).Returns(projectionResult);

        var filterBuilder = Substitute.For<IFilterExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Total > 100;

        var filterResult = new FilterExpressionResult(
            "#filt_0 > :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Total" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { N = "100" } });

        filterBuilder.BuildFilter(predicate).Returns(filterResult);

        // Act
        var result = request
            .WithProjection(projectionBuilder, selector)
            .WithFilter(filterBuilder, predicate);

        // Assert
        result.Should().BeSameAs(request);
        result.ProjectionExpression.Should().Be("OrderId, #proj_0");
        result.FilterExpression.Should().Be("#filt_0 > :filt_v0");

        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#proj_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");

        result.ExpressionAttributeValues.Should().HaveCount(1);
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("100");
    }

    [Fact]
    public void FluentChaining_KeyCondition_Projection_Filter_AllApplied()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };

        var keyConditionBuilder = Substitute.For<IKeyConditionExpressionBuilder<Order>>();
        var keyConditionResult = new KeyConditionExpressionResult(
            "#key_0 = :key_v0",
            new Dictionary<string, string> { ["#key_0"] = "OrderId" },
            new Dictionary<string, AttributeValue> { [":key_v0"] = new AttributeValue { S = "ORDER-123" } });

        var projectionBuilder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, object>> selector = o => new { o.OrderId, o.Total };

        var projectionResult = new ProjectionResult(
            "OrderId, Total",
            new Dictionary<string, string>(),
            Array.Empty<PropertyPath>(),
            ProjectionShape.Composite,
            new[] { "OrderId", "Total" });

        projectionBuilder.BuildProjection(selector).Returns(projectionResult);

        var filterBuilder = Substitute.For<IFilterExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Status == "Active";

        var filterResult = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { S = "Active" } });

        filterBuilder.BuildFilter(predicate).Returns(filterResult);

        // Act
        var result = request
            .WithKeyCondition(keyConditionBuilder, b => keyConditionResult)
            .WithProjection(projectionBuilder, selector)
            .WithFilter(filterBuilder, predicate);

        // Assert
        result.Should().BeSameAs(request);
        result.KeyConditionExpression.Should().Be("#key_0 = :key_v0");
        result.ProjectionExpression.Should().Be("OrderId, Total");
        result.FilterExpression.Should().Be("#filt_0 = :filt_v0");

        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#key_0"].Should().Be("OrderId");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Status");

        result.ExpressionAttributeValues.Should().HaveCount(2);
        result.ExpressionAttributeValues[":key_v0"].S.Should().Be("ORDER-123");
        result.ExpressionAttributeValues[":filt_v0"].S.Should().Be("Active");
    }

    [Fact]
    public void FluentChaining_AllScopes_AliasesDoNotCollide()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };

        // Key condition with #key_ and :key_v prefixes
        var keyConditionBuilder = Substitute.For<IKeyConditionExpressionBuilder<Order>>();
        var keyConditionResult = new KeyConditionExpressionResult(
            "#key_0 = :key_v0 AND begins_with(#key_1, :key_v1)",
            new Dictionary<string, string>
            {
                ["#key_0"] = "PK",
                ["#key_1"] = "SK"
            },
            new Dictionary<string, AttributeValue>
            {
                [":key_v0"] = new AttributeValue { S = "USER#123" },
                [":key_v1"] = new AttributeValue { S = "ORDER#" }
            });

        // Projection with #proj_ prefix
        var projectionBuilder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, object>> selector = o => new { o.OrderId, o.Status };

        var projectionResult = new ProjectionResult(
            "OrderId, #proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "Status" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.Composite,
            new[] { "OrderId", "Status" });

        projectionBuilder.BuildProjection(selector).Returns(projectionResult);

        // Filter with #filt_ and :filt_v prefixes
        var filterBuilder = Substitute.For<IFilterExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Total > 100;

        var filterResult = new FilterExpressionResult(
            "#filt_0 > :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Total" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { N = "100" } });

        filterBuilder.BuildFilter(predicate).Returns(filterResult);

        // Act
        var result = request
            .WithKeyCondition(keyConditionBuilder, b => keyConditionResult)
            .WithProjection(projectionBuilder, selector)
            .WithFilter(filterBuilder, predicate);

        // Assert - verify all aliases are distinct and present
        result.ExpressionAttributeNames.Should().HaveCount(4);
        result.ExpressionAttributeNames.Keys.Should().BeEquivalentTo(
            "#key_0", "#key_1", "#proj_0", "#filt_0");

        result.ExpressionAttributeValues.Should().HaveCount(3);
        result.ExpressionAttributeValues.Keys.Should().BeEquivalentTo(
            ":key_v0", ":key_v1", ":filt_v0");

        // Verify no collision occurred by checking values are correct
        result.ExpressionAttributeNames["#key_0"].Should().Be("PK");
        result.ExpressionAttributeNames["#key_1"].Should().Be("SK");
        result.ExpressionAttributeNames["#proj_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");

        result.ExpressionAttributeValues[":key_v0"].S.Should().Be("USER#123");
        result.ExpressionAttributeValues[":key_v1"].S.Should().Be("ORDER#");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("100");
    }
}
