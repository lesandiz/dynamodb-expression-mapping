using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;
using NSubstitute;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for ConditionExtensions (Spec 10 §3).
/// </summary>
public class ConditionExtensionsTests
{
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public void PutItemRequest_WithCondition_SetsConditionExpression()
    {
        // Arrange
        var request = new PutItemRequest { TableName = "Orders" };
        var builder = Substitute.For<IConditionExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Status == "Draft";

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Draft" } });

        builder.BuildCondition(predicate).Returns(conditionResult);

        // Act
        var result = request.WithCondition(builder, predicate);

        // Assert
        result.Should().BeSameAs(request);
        result.ConditionExpression.Should().Be("#cond_0 = :cond_v0");
    }

    [Fact]
    public void PutItemRequest_WithCondition_MergesAttributeNamesAndValues()
    {
        // Arrange
        var request = new PutItemRequest
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
        var builder = Substitute.For<IConditionExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Total < 1000;

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 < :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Total" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { N = "1000" } });

        builder.BuildCondition(predicate).Returns(conditionResult);

        // Act
        var result = request.WithCondition(builder, predicate);

        // Assert
        result.ExpressionAttributeNames.Should().HaveCount(2);
        result.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        result.ExpressionAttributeNames["#cond_0"].Should().Be("Total");

        result.ExpressionAttributeValues.Should().HaveCount(2);
        result.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
        result.ExpressionAttributeValues[":cond_v0"].N.Should().Be("1000");
    }

    [Fact]
    public void DeleteItemRequest_WithCondition_SetsConditionExpression()
    {
        // Arrange
        var request = new DeleteItemRequest { TableName = "Orders" };
        var builder = Substitute.For<IConditionExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Status == "Cancelled";

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 = :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { S = "Cancelled" } });

        builder.BuildCondition(predicate).Returns(conditionResult);

        // Act
        var result = request.WithCondition(builder, predicate);

        // Assert
        result.Should().BeSameAs(request);
        result.ConditionExpression.Should().Be("#cond_0 = :cond_v0");
        result.ExpressionAttributeNames.Should().ContainKey("#cond_0");
        result.ExpressionAttributeValues.Should().ContainKey(":cond_v0");
    }

    [Fact]
    public void UpdateItemRequest_WithCondition_SetsConditionExpression()
    {
        // Arrange
        var request = new UpdateItemRequest { TableName = "Orders" };
        var builder = Substitute.For<IConditionExpressionBuilder<Order>>();
        Expression<Func<Order, bool>> predicate = o => o.Total > 0;

        var conditionResult = new ConditionExpressionResult(
            "#cond_0 > :cond_v0",
            new Dictionary<string, string> { ["#cond_0"] = "Total" },
            new Dictionary<string, AttributeValue> { [":cond_v0"] = new AttributeValue { N = "0" } });

        builder.BuildCondition(predicate).Returns(conditionResult);

        // Act
        var result = request.WithCondition(builder, predicate);

        // Assert
        result.Should().BeSameAs(request);
        result.ConditionExpression.Should().Be("#cond_0 > :cond_v0");
        result.ExpressionAttributeNames.Should().HaveCount(1);
        result.ExpressionAttributeValues.Should().HaveCount(1);
    }

    [Fact]
    public void WithCondition_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new PutItemRequest { TableName = "Orders" };
        Expression<Func<Order, bool>> predicate = o => o.Status == "Active";

        // Act
        var act = () => request.WithCondition<Order>(null!, predicate);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("conditionBuilder");
    }
}
