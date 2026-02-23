using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using FluentAssertions;

namespace DynamoDb.ExpressionMapping.Tests.Extensions;

/// <summary>
/// Tests for UpdateExtensions (Spec 10 §5).
/// </summary>
public class UpdateExtensionsTests
{
    public class Order
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    [Fact]
    public void UpdateItemRequest_WithUpdate_SetsUpdateExpression()
    {
        // Arrange
        var request = new UpdateItemRequest { TableName = "Orders" };
        var updateResult = new UpdateExpressionResult(
            "SET #upd_0 = :upd_v0",
            new Dictionary<string, string> { ["#upd_0"] = "Status" },
            new Dictionary<string, AttributeValue> { [":upd_v0"] = new AttributeValue { S = "Completed" } });

        // Act
        var result = request.WithUpdate(updateResult);

        // Assert
        result.Should().BeSameAs(request);
        result.UpdateExpression.Should().Be("SET #upd_0 = :upd_v0");
    }

    [Fact]
    public void UpdateItemRequest_WithUpdate_MergesAttributeNamesAndValues()
    {
        // Arrange
        var request = new UpdateItemRequest
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

        var updateResult = new UpdateExpressionResult(
            "SET #upd_0 = :upd_v0, #upd_1 = :upd_v1",
            new Dictionary<string, string>
            {
                ["#upd_0"] = "Status",
                ["#upd_1"] = "Total"
            },
            new Dictionary<string, AttributeValue>
            {
                [":upd_v0"] = new AttributeValue { S = "Shipped" },
                [":upd_v1"] = new AttributeValue { N = "150" }
            });

        // Act
        var result = request.WithUpdate(updateResult);

        // Assert
        result.ExpressionAttributeNames.Should().HaveCount(3);
        result.ExpressionAttributeNames["#existing"].Should().Be("ExistingAttr");
        result.ExpressionAttributeNames["#upd_0"].Should().Be("Status");
        result.ExpressionAttributeNames["#upd_1"].Should().Be("Total");

        result.ExpressionAttributeValues.Should().HaveCount(3);
        result.ExpressionAttributeValues[":existing"].S.Should().Be("ExistingValue");
        result.ExpressionAttributeValues[":upd_v0"].S.Should().Be("Shipped");
        result.ExpressionAttributeValues[":upd_v1"].N.Should().Be("150");
    }

    [Fact]
    public void WithUpdate_EmptyResult_NoOp()
    {
        // Arrange
        var request = new UpdateItemRequest { TableName = "Orders" };
        var initialNamesState = request.ExpressionAttributeNames;
        var initialValuesState = request.ExpressionAttributeValues;
        var emptyResult = UpdateExpressionResult.Empty;

        // Act
        var result = request.WithUpdate(emptyResult);

        // Assert
        result.Should().BeSameAs(request);
        result.UpdateExpression.Should().BeNull();
        // Empty result should not modify the request's dictionaries
        result.ExpressionAttributeNames.Should().BeSameAs(initialNamesState);
        result.ExpressionAttributeValues.Should().BeSameAs(initialValuesState);
    }

}
