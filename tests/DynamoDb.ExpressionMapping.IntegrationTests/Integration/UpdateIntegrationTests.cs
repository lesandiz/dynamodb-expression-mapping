using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.IntegrationTests.Integration;

/// <summary>
/// Integration tests for UpdateExpressionBuilder verifying that generated expressions
/// are accepted by DynamoDB and apply the expected changes.
/// </summary>
[Collection("DynamoDb")]
[Trait("Category", "Integration")]
public class UpdateIntegrationTests : IAsyncLifetime
{
    private readonly DynamoDbFixture _fixture;
    private readonly string _tableName;
    private UpdateExpressionBuilder<TestIntegrationEntity> _updateBuilder = null!;

    public UpdateIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
        _tableName = $"UpdateTests_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        await _fixture.CreateTableAsync(_tableName, "Id", ScalarAttributeType.S);

        // Initialize update builder with default configuration
        var resolverFactory = new AttributeNameResolverFactory();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        _updateBuilder = new UpdateExpressionBuilder<TestIntegrationEntity>(resolverFactory, converterRegistry);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DeleteTableAsync(_tableName);
    }

    /// <summary>
    /// Test 1: MultiClauseUpdate_AllClausesApplied
    /// Verifies that SET, REMOVE, ADD, and DELETE clauses can be combined in a single UpdateExpression.
    /// </summary>
    [Fact]
    public async Task MultiClauseUpdate_AllClausesApplied()
    {
        // Arrange: Create an item with various attribute types
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Original Name",
            count: 10,
            tags: new List<string> { "tag1", "tag2" },
            categories: new HashSet<string> { "cat1", "cat2", "cat3" });

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act: Build update expression with all clause types:
        // SET: Update Name
        // REMOVE: Remove Tags
        // ADD: Increment Count by 5
        // DELETE: Remove "cat2" from Categories
        var updateResult = new UpdateExpressionBuilder<TestIntegrationEntity>(
                new AttributeNameResolverFactory(),
                AttributeValueConverterRegistry.Default)
            .Set(e => e.Name, "Updated Name")
            .Remove(e => e.Tags)
            .Add(e => e.Count, 5)
            .Delete(e => e.Categories, new HashSet<string> { "cat2" })
            .Build();

        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithUpdate(updateResult);

        await _fixture.Client.UpdateItemAsync(updateRequest);

        // Assert: Retrieve the item and verify all changes were applied
        var getResponse = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        var updatedItem = getResponse.Item;

        // SET: Name should be updated
        updatedItem["Name"].S.Should().Be("Updated Name");

        // REMOVE: Tags should be removed
        updatedItem.Should().NotContainKey("Tags");

        // ADD: Count should be incremented by 5 (10 + 5 = 15)
        updatedItem["Count"].N.Should().Be("15");

        // DELETE: Categories should have cat2 removed, leaving cat1 and cat3
        updatedItem["Categories"].SS.Should().HaveCount(2);
        updatedItem["Categories"].SS.Should().Contain("cat1");
        updatedItem["Categories"].SS.Should().Contain("cat3");
        updatedItem["Categories"].SS.Should().NotContain("cat2");
    }

    /// <summary>
    /// Test 2: SetIfNotExists_PreservesExistingValue
    /// Verifies that if_not_exists function only sets the value if the attribute doesn't exist.
    /// </summary>
    [Fact]
    public async Task SetIfNotExists_PreservesExistingValue()
    {
        // Arrange: Create an item with OptionalScore set
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            optionalScore: 100);

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act: Try to set OptionalScore using if_not_exists - should preserve existing value
        var updateResult = new UpdateExpressionBuilder<TestIntegrationEntity>(
                new AttributeNameResolverFactory(),
                AttributeValueConverterRegistry.Default)
            .SetIfNotExists(e => e.OptionalScore, 50)
            .Build();

        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithUpdate(updateResult);

        await _fixture.Client.UpdateItemAsync(updateRequest);

        // Assert: OptionalScore should still be 100 (existing value preserved)
        var getResponse = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        getResponse.Item["OptionalScore"].N.Should().Be("100");

        // Now create a new item without OptionalScore and verify if_not_exists sets it
        var testId2 = Guid.NewGuid();
        var item2 = AttributeValueFixtures.CreateTestEntityItem(
            id: testId2,
            optionalScore: null);

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item2
        });

        var updateResult2 = new UpdateExpressionBuilder<TestIntegrationEntity>(
                new AttributeNameResolverFactory(),
                AttributeValueConverterRegistry.Default)
            .SetIfNotExists(e => e.OptionalScore, 50)
            .Build();

        var updateRequest2 = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId2.ToString() }
            }
        }.WithUpdate(updateResult2);

        await _fixture.Client.UpdateItemAsync(updateRequest2);

        // Assert: OptionalScore should now be 50 (newly set value)
        var getResponse2 = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId2.ToString() }
            }
        });

        getResponse2.Item["OptionalScore"].N.Should().Be("50");
    }

    /// <summary>
    /// Test 3: AppendToList_AppendsElements
    /// Verifies that list_append function appends new elements to an existing list.
    /// </summary>
    [Fact]
    public async Task AppendToList_AppendsElements()
    {
        // Arrange: Create an item with a Tags list
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            tags: new List<string> { "tag1", "tag2" });

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act: Append new tags to the list
        var updateResult = new UpdateExpressionBuilder<TestIntegrationEntity>(
                new AttributeNameResolverFactory(),
                AttributeValueConverterRegistry.Default)
            .AppendToList(e => e.Tags, new List<string> { "tag3", "tag4" })
            .Build();

        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithUpdate(updateResult);

        await _fixture.Client.UpdateItemAsync(updateRequest);

        // Assert: Tags should now contain all four tags in order
        var getResponse = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        var tags = getResponse.Item["Tags"].L.Select(av => av.S).ToList();
        tags.Should().HaveCount(4);
        tags.Should().ContainInOrder("tag1", "tag2", "tag3", "tag4");
    }

    /// <summary>
    /// Test 4: AddToNumber_IncrementsValue
    /// Verifies that ADD clause increments a numeric attribute.
    /// </summary>
    [Fact]
    public async Task AddToNumber_IncrementsValue()
    {
        // Arrange: Create an item with Count = 10
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            count: 10);

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act: Use ADD to increment Count by 25
        var updateResult = new UpdateExpressionBuilder<TestIntegrationEntity>(
                new AttributeNameResolverFactory(),
                AttributeValueConverterRegistry.Default)
            .Add(e => e.Count, 25)
            .Build();

        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithUpdate(updateResult);

        await _fixture.Client.UpdateItemAsync(updateRequest);

        // Assert: Count should be 35 (10 + 25)
        var getResponse = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        getResponse.Item["Count"].N.Should().Be("35");

        // Test negative increment (decrement)
        var updateResult2 = new UpdateExpressionBuilder<TestIntegrationEntity>(
                new AttributeNameResolverFactory(),
                AttributeValueConverterRegistry.Default)
            .Add(e => e.Count, -15)
            .Build();

        var updateRequest2 = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithUpdate(updateResult2);

        await _fixture.Client.UpdateItemAsync(updateRequest2);

        // Assert: Count should be 20 (35 - 15)
        var getResponse2 = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        getResponse2.Item["Count"].N.Should().Be("20");
    }

    /// <summary>
    /// Test 5: DeleteFromSet_RemovesElements
    /// Verifies that DELETE clause removes specific elements from a string set.
    /// </summary>
    [Fact]
    public async Task DeleteFromSet_RemovesElements()
    {
        // Arrange: Create an item with Categories string set
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            categories: new HashSet<string> { "electronics", "computers", "laptops", "gaming" });

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act: Delete specific categories from the set
        var updateResult = new UpdateExpressionBuilder<TestIntegrationEntity>(
                new AttributeNameResolverFactory(),
                AttributeValueConverterRegistry.Default)
            .Delete(e => e.Categories, new HashSet<string> { "computers", "gaming" })
            .Build();

        var updateRequest = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithUpdate(updateResult);

        await _fixture.Client.UpdateItemAsync(updateRequest);

        // Assert: Categories should now only contain "electronics" and "laptops"
        var getResponse = await _fixture.Client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        });

        var categories = getResponse.Item["Categories"].SS;
        categories.Should().HaveCount(2);
        categories.Should().Contain("electronics");
        categories.Should().Contain("laptops");
        categories.Should().NotContain("computers");
        categories.Should().NotContain("gaming");
    }
}
