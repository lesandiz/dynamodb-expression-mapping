using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;
using NSubstitute;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for KeyConditionExtensions (Spec 10 §4).
/// </summary>
public class KeyConditionExtensionsTests
{
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public void QueryRequest_WithKeyCondition_SetsKeyConditionExpression()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };
        var builder = Substitute.For<IKeyConditionExpressionBuilder<Order>>();

        var keyConditionResult = new KeyConditionExpressionResult(
            "#key_0 = :key_v0",
            new Dictionary<string, string> { ["#key_0"] = "OrderId" },
            new Dictionary<string, AttributeValue> { [":key_v0"] = new AttributeValue { S = "ORDER-123" } });

        // Act
        var result = request.WithKeyCondition(builder, b =>
        {
            // Simulate the staged fluent API returning the result
            return keyConditionResult;
        });

        // Assert
        result.Should().BeSameAs(request);
        result.KeyConditionExpression.Should().Be("#key_0 = :key_v0");
    }

    [Fact]
    public void QueryRequest_WithKeyCondition_MergesAttributeNamesAndValues()
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
        var builder = Substitute.For<IKeyConditionExpressionBuilder<Order>>();

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

        // Act
        var result = request.WithKeyCondition(builder, b => keyConditionResult);

        // Assert
        result.ExpressionAttributeNames.Should().HaveCount(3);
        result.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        result.ExpressionAttributeNames["#key_0"].Should().Be("PK");
        result.ExpressionAttributeNames["#key_1"].Should().Be("SK");

        result.ExpressionAttributeValues.Should().HaveCount(3);
        result.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
        result.ExpressionAttributeValues[":key_v0"].S.Should().Be("USER#123");
        result.ExpressionAttributeValues[":key_v1"].S.Should().Be("ORDER#");
    }

    [Fact]
    public void WithKeyCondition_NullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };

        // Act
        var act = () => request.WithKeyCondition<Order>(
            null!,
            b => new KeyConditionExpressionResult(
                "PK = :v0",
                new Dictionary<string, string>(),
                new Dictionary<string, AttributeValue>()));

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("keyConditionBuilder");
    }

    [Fact]
    public void WithKeyCondition_NullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var request = new QueryRequest { TableName = "Orders" };
        var builder = Substitute.For<IKeyConditionExpressionBuilder<Order>>();

        // Act
        var act = () => request.WithKeyCondition(builder, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }
}
