using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for FilterExtensions (Spec 10 §2).
/// </summary>
public class FilterExtensionsTests
{
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public void QueryRequest_WithFilter_SetsFilterExpression()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };
        var builder = Substitute.For<IFilterExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Status == "Active";

        var filterResult = new FilterExpressionResult(
            "#filt_0 = :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { S = "Active" } });

        builder.BuildFilter(predicate).Returns(filterResult);

        // Act
        var result = request.WithFilter(builder, predicate);

        // Assert
        result.Should().BeSameAs(request);
        result.FilterExpression.Should().Be("#filt_0 = :filt_v0");
    }

    [Fact]
    public void QueryRequest_WithFilter_MergesAttributeNamesAndValues()
    {
        // Arrange
        var request = new QueryRequest
        {
            TableName = "Orders",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":existing"] = new AttributeValue { S = "ExistingValue" }
            }
        };
        var builder = Substitute.For<IFilterExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Total > 100;

        var filterResult = new FilterExpressionResult(
            "#filt_0 > :filt_v0",
            new Dictionary<string, string> { ["#filt_0"] = "Total" },
            new Dictionary<string, AttributeValue> { [":filt_v0"] = new AttributeValue { N = "100" } });

        builder.BuildFilter(predicate).Returns(filterResult);

        // Act
        var result = request.WithFilter(builder, predicate);

        // Assert
        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        result.ExpressionAttributeNames["#filt_0"].Should().Be("Total");

        result.ExpressionAttributeValues.Should().HaveCount(2);
        result.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
        result.ExpressionAttributeValues[":filt_v0"].N.Should().Be("100");
    }

    [Fact]
    public void ScanRequest_WithFilter_SetsFilterExpression()
    {
        // Arrange
        var request = new ScanRequest { TableName = "Orders" };
        var builder = Substitute.For<IFilterExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Status == "Completed" && o.Total > 50;

        var filterResult = new FilterExpressionResult(
            "#filt_0 = :filt_v0 AND #filt_1 > :filt_v1",
            new Dictionary<string, string>
            {
                ["#filt_0"] = "Status",
                ["#filt_1"] = "Total"
            },
            new Dictionary<string, AttributeValue>
            {
                [":filt_v0"] = new AttributeValue { S = "Completed" },
                [":filt_v1"] = new AttributeValue { N = "50" }
            });

        builder.BuildFilter(predicate).Returns(filterResult);

        // Act
        var result = request.WithFilter(builder, predicate);

        // Assert
        result.Should().BeSameAs(request);
        result.FilterExpression.Should().Be("#filt_0 = :filt_v0 AND #filt_1 > :filt_v1");
        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeValues.Should().HaveCount(2);
    }

    [Fact]
    public void FluentChaining_ProjectionAndFilter()
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
}
