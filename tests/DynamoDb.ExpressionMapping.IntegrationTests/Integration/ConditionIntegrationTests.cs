using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.IntegrationTests.Integration;

/// <summary>
/// Integration tests for condition expressions with DynamoDB.
/// Verifies that conditions prevent writes when they fail and allow writes when they pass.
/// </summary>
[Collection("DynamoDb")]
[Trait("Category", "Integration")]
public class ConditionIntegrationTests : IAsyncLifetime
{
    private readonly DynamoDbFixture _fixture;
    private readonly string _tableName;
    private readonly IAmazonDynamoDB _client;
    private readonly ConditionExpressionBuilder<TestIntegrationEntity> _conditionBuilder;

    public ConditionIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
        _tableName = $"ConditionIntegrationTests_{Guid.NewGuid():N}";

        // Initialize condition builder
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _conditionBuilder = new ConditionExpressionBuilder<TestIntegrationEntity>(
            resolverFactory,
            AttributeValueConverterRegistry.Default);
    }

    public async Task InitializeAsync()
    {
        // Create table with Id as partition key
        await _fixture.CreateTableAsync(_tableName, "Id", ScalarAttributeType.S);
    }

    public async Task DisposeAsync()
    {
        // Clean up table
        await _fixture.DeleteTableAsync(_tableName);
    }

    /// <summary>
    /// Test 1: ConditionExpression prevents write when condition fails.
    /// Should throw ConditionalCheckFailedException.
    /// </summary>
    [Fact]
    public async Task ConditionFails_ThrowsConditionalCheckFailedException()
    {
        // Arrange - Create an item with Count = 10
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Test Item",
            count: 10);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Try to update the item with a condition that fails (Count must be 20, but it's 10)
        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            },
            UpdateExpression = "SET #n = :newName",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#n"] = "Name"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":newName"] = new AttributeValue { S = "Updated Name" }
            }
        }.WithCondition(_conditionBuilder, (TestIntegrationEntity e) => e.Count == 20);

        // Assert - Should throw ConditionalCheckFailedException
        var act = async () => await _client.UpdateItemAsync(updateRequest);
        await act.Should().ThrowAsync<ConditionalCheckFailedException>();

        // Verify item was not modified
        var getResponse = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        getResponse.Item["Name"].S.Should().Be("Test Item"); // Name should still be original
        getResponse.Item["Count"].N.Should().Be("10"); // Count should still be 10
    }

    /// <summary>
    /// Test 2: ConditionExpression allows write when condition passes.
    /// Should succeed and update the item.
    /// </summary>
    [Fact]
    public async Task ConditionPasses_WriteSucceeds()
    {
        // Arrange - Create an item with Count = 10
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Test Item",
            count: 10);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Update the item with a condition that passes (Count must be 10, and it is)
        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            },
            UpdateExpression = "SET #n = :newName",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#n"] = "Name"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":newName"] = new AttributeValue { S = "Updated Name" }
            }
        }.WithCondition(_conditionBuilder, (TestIntegrationEntity e) => e.Count == 10);

        updateRequest.ConditionExpression.Should().NotBeNullOrEmpty("condition should be applied to request");

        var updateResponse = await _client.UpdateItemAsync(updateRequest);

        // Assert - Update should succeed
        updateResponse.Should().NotBeNull();

        // Verify item was modified
        var getResponse = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        getResponse.Item["Name"].S.Should().Be("Updated Name"); // Name should be updated
        getResponse.Item["Count"].N.Should().Be("10"); // Count should still be 10
    }

    /// <summary>
    /// Test 3: Condition on DeleteItemRequest prevents deletion when condition fails.
    /// Should throw ConditionalCheckFailedException and item should remain.
    /// </summary>
    [Fact]
    public async Task DeleteWithCondition_ConditionFails_ItemNotDeleted()
    {
        // Arrange - Create an item with Enabled = true
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Test Item",
            enabled: true);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Try to delete the item with a condition that fails (Enabled must be false, but it's true)
        var deleteRequest = new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithCondition(_conditionBuilder, (TestIntegrationEntity e) => !e.Enabled);

        // Assert - Should throw ConditionalCheckFailedException
        var act = async () => await _client.DeleteItemAsync(deleteRequest);
        await act.Should().ThrowAsync<ConditionalCheckFailedException>();

        // Verify item was NOT deleted
        var getResponse = await _client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        getResponse.Item.Should().NotBeNull(); // Item should still exist
        getResponse.Item["Name"].S.Should().Be("Test Item");
        getResponse.Item["Enabled"].BOOL.Should().BeTrue();
    }
}
