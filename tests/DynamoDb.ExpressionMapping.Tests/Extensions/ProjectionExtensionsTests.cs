using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for ProjectionExtensions (Spec 10 §1).
/// </summary>
public class ProjectionExtensionsTests
{
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public void GetItemRequest_WithProjection_SetsProjectionExpression()
    {
        // Arrange
        var request = new GetItemRequest { TableName = "Orders" };
        var builder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, object>> selector = o => new { o.OrderId, o.Total };

        var projectionResult = new ProjectionResult(
            "OrderId, Total",
            new Dictionary<string, string>(),
            Array.Empty<PropertyPath>(),
            ProjectionShape.Composite,
            new[] { "OrderId", "Total" });

        builder.BuildProjection(selector).Returns(projectionResult);

        // Act
        var result = request.WithProjection(builder, selector);

        // Assert
        result.Should().BeSameAs(request);
        result.ProjectionExpression.Should().Be("OrderId, Total");
    }

    [Fact]
    public void GetItemRequest_WithProjection_MergesAttributeNames()
    {
        // Arrange
        var request = new GetItemRequest
        {
            TableName = "Orders",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#existing"] = "ExistingAttr"
            }
        };
        var builder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, string>> selector = o => o.Status;

        var projectionResult = new ProjectionResult(
            "#proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "Status" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "Status" });

        builder.BuildProjection(selector).Returns(projectionResult);

        // Act
        var result = request.WithProjection(builder, selector);

        // Assert
        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        result.ExpressionAttributeNames["#proj_0"].Should().Be("Status");
    }

    [Fact]
    public void QueryRequest_WithProjection_SetsProjectionExpression()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };
        var builder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, object>> selector = o => new { o.OrderId, o.Status };

        var projectionResult = new ProjectionResult(
            "OrderId, #proj_0",
            new Dictionary<string, string> { ["#proj_0"] = "Status" },
            Array.Empty<PropertyPath>(),
            ProjectionShape.Composite,
            new[] { "OrderId", "Status" });

        builder.BuildProjection(selector).Returns(projectionResult);

        // Act
        var result = request.WithProjection(builder, selector);

        // Assert
        result.Should().BeSameAs(request);
        result.ProjectionExpression.Should().Be("OrderId, #proj_0");
        result.ExpressionAttributeNames.Should().ContainKey("#proj_0");
    }

    [Fact]
    public void QueryRequest_WithProjection_NullSelector_NoOp()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };
        var builder = Substitute.For<IProjectionBuilder<Order>>();

        // Act
        var result = request.WithProjection<Order, object>(builder, null);

        // Assert
        result.Should().BeSameAs(request);
        result.ProjectionExpression.Should().BeNull();
        builder.DidNotReceive().BuildProjection(Arg.Any<Expression<Func<Order, object>>>());
    }

    [Fact]
    public void ScanRequest_WithProjection_SetsProjectionExpression()
    {
        // Arrange
        var request = new ScanRequest { TableName = "Orders" };
        var builder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, decimal>> selector = o => o.Total;

        var projectionResult = new ProjectionResult(
            "Total",
            new Dictionary<string, string>(),
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "Total" });

        builder.BuildProjection(selector).Returns(projectionResult);

        // Act
        var result = request.WithProjection(builder, selector);

        // Assert
        result.Should().BeSameAs(request);
        result.ProjectionExpression.Should().Be("Total");
    }

    [Fact]
    public void ScanRequest_WithProjection_NullSelector_NoOp()
    {
        // Arrange
        var request = new ScanRequest { TableName = "Orders" };
        var builder = Substitute.For<IProjectionBuilder<Order>>();

        // Act
        var result = request.WithProjection<Order, object>(builder, null);

        // Assert
        result.Should().BeSameAs(request);
        result.ProjectionExpression.Should().BeNull();
        builder.DidNotReceive().BuildProjection(Arg.Any<Expression<Func<Order, object>>>());
    }

    [Fact]
    public void BatchGetItemRequest_WithProjection_SetsProjectionOnTable()
    {
        // Arrange
        var request = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                ["Orders"] = new KeysAndAttributes
                {
                    Keys = new List<Dictionary<string, AttributeValue>>
                    {
                        new() { ["OrderId"] = new AttributeValue { S = "123" } }
                    }
                }
            }
        };
        var builder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, object>> selector = o => new { o.OrderId, o.Total };

        var projectionResult = new ProjectionResult(
            "OrderId, Total",
            new Dictionary<string, string>(),
            Array.Empty<PropertyPath>(),
            ProjectionShape.Composite,
            new[] { "OrderId", "Total" });

        builder.BuildProjection(selector).Returns(projectionResult);

        // Act
        var result = request.WithProjection("Orders", builder, selector);

        // Assert
        result.Should().BeSameAs(request);
        result.RequestItems["Orders"].ProjectionExpression.Should().Be("OrderId, Total");
    }

    [Fact]
    public void BatchGetItemRequest_WithProjection_TableNotFound_ThrowsArgumentException()
    {
        // Arrange
        var request = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                ["Products"] = new KeysAndAttributes()
            }
        };
        var builder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, string>> selector = o => o.OrderId;

        // Act
        var act = () => request.WithProjection("Orders", builder, selector);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Table 'Orders' not found in RequestItems*")
            .And.ParamName.Should().Be("tableName");
    }

    [Fact]
    public void BatchGetItemRequest_WithProjection_NullRequestItems_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new BatchGetItemRequest { RequestItems = null! };
        var builder = Substitute.For<IProjectionBuilder<Order>>();
        Expression<Func<Order, string>> selector = o => o.OrderId;

        var projectionResult = new ProjectionResult(
            "OrderId",
            new Dictionary<string, string>(),
            Array.Empty<PropertyPath>(),
            ProjectionShape.SingleProperty,
            new[] { "OrderId" });

        builder.BuildProjection(selector).Returns(projectionResult);

        // Act
        var act = () => request.WithProjection("Orders", builder, selector);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithProjection_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new GetItemRequest { TableName = "Orders" };
        Expression<Func<Order, string>> selector = o => o.OrderId;

        // Act
        var act = () => request.WithProjection<Order, string>(null!, selector);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("projectionBuilder");
    }
}
