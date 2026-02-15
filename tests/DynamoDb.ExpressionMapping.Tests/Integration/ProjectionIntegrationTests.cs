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

namespace DynamoDb.ExpressionMapping.Tests.Integration;

/// <summary>
/// Integration tests for projection expressions with DynamoDB.
/// Verifies that generated expressions are accepted by DynamoDB and return expected results.
/// </summary>
[Collection("DynamoDb")]
[Trait("Category", "Integration")]
public class ProjectionIntegrationTests : IAsyncLifetime
{
    private readonly DynamoDbFixture _fixture;
    private readonly string _tableName;
    private readonly IAmazonDynamoDB _client;
    private readonly ProjectionBuilder<TestIntegrationEntity> _projectionBuilder;
    private readonly FilterExpressionBuilder<TestIntegrationEntity> _filterBuilder;

    public ProjectionIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
        _tableName = $"ProjectionIntegrationTests_{Guid.NewGuid():N}";

        // Initialize builders
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _projectionBuilder = new ProjectionBuilder<TestIntegrationEntity>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            NullExpressionCache.Instance);
        _filterBuilder = new FilterExpressionBuilder<TestIntegrationEntity>(
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
    /// Test 1: Reserved keyword attributes are aliased and DynamoDB returns only projected fields.
    /// </summary>
    [Fact]
    public async Task ReservedKeywordProjection_ReturnsOnlyProjectedAttributes()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Reserved Name Test",
            count: 42);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Project only Name (reserved keyword) and Count
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new { p.Name, p.Count });

        var response = await _client.ScanAsync(scanRequest);

        // Assert
        response.Items.Should().HaveCount(1);
        var returnedItem = response.Items[0];

        // Should only contain projected attributes
        returnedItem.Should().ContainKey("Name");
        returnedItem.Should().ContainKey("Count");
        returnedItem["Name"].S.Should().Be("Reserved Name Test");
        returnedItem["Count"].N.Should().Be("42");

        // Should NOT contain other attributes
        returnedItem.Should().NotContainKey("Id");
        returnedItem.Should().NotContainKey("Status");
        returnedItem.Should().NotContainKey("Enabled");
    }

    /// <summary>
    /// Test 2: Nested path "Address.City" is accepted and returns the nested value.
    /// </summary>
    [Fact]
    public async Task NestedPropertyProjection_ReturnsDottedPath()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateNestedEntityItem(
            id: testId,
            city: "Manchester",
            postCode: "M1 1AA",
            floor: 5);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Project nested property Address.City
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new { p.Address!.City });

        var response = await _client.ScanAsync(scanRequest);

        // Assert
        response.Items.Should().HaveCount(1);
        var returnedItem = response.Items[0];

        // Should contain nested Address with only City
        returnedItem.Should().ContainKey("Address");
        returnedItem["Address"].M.Should().ContainKey("City");
        returnedItem["Address"].M["City"].S.Should().Be("Manchester");

        // Address should NOT contain PostCode or Floor
        returnedItem["Address"].M.Should().NotContainKey("PostCode");
        returnedItem["Address"].M.Should().NotContainKey("Floor");

        // Should NOT contain top-level attributes
        returnedItem.Should().NotContainKey("Name");
        returnedItem.Should().NotContainKey("Count");
    }

    /// <summary>
    /// Test 3: Projection + filter combined on same request with merged alias maps.
    /// </summary>
    [Fact]
    public async Task ProjectionWithFilter_MergedAliases_Accepted()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var item1 = AttributeValueFixtures.CreateTestEntityItem(
            id: id1,
            name: "Active Item",
            count: 10,
            enabled: true);

        var item2 = AttributeValueFixtures.CreateTestEntityItem(
            id: id2,
            name: "Inactive Item",
            count: 20,
            enabled: false);

        await _client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });

        // Act - Project Name and Count, filter by Enabled
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new { p.Name, p.Count })
            .WithFilter(_filterBuilder, (TestIntegrationEntity p) => p.Enabled);

        var response = await _client.ScanAsync(scanRequest);

        // Assert - Should return only the enabled item with projected attributes
        response.Items.Should().HaveCount(1);
        var returnedItem = response.Items[0];

        returnedItem["Name"].S.Should().Be("Active Item");
        returnedItem["Count"].N.Should().Be("10");

        // Should NOT contain other attributes
        returnedItem.Should().NotContainKey("Id");
        returnedItem.Should().NotContainKey("Enabled");
        returnedItem.Should().NotContainKey("Status");
    }

    /// <summary>
    /// Test 4: GetItemRequest with projection returns only specified attributes.
    /// </summary>
    [Fact]
    public async Task GetItemRequest_WithProjection_ReturnsProjectedAttributes()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "GetItem Test",
            count: 100,
            enabled: true);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - GetItem with projection
        var getRequest = new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId.ToString() }
            }
        }.WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new { p.Name, p.Count, p.Enabled });

        var response = await _client.GetItemAsync(getRequest);

        // Assert
        response.Item.Should().NotBeNull();
        response.Item.Should().ContainKey("Name");
        response.Item.Should().ContainKey("Count");
        response.Item.Should().ContainKey("Enabled");

        response.Item["Name"].S.Should().Be("GetItem Test");
        response.Item["Count"].N.Should().Be("100");
        response.Item["Enabled"].BOOL.Should().BeTrue();

        // Should NOT contain Id or other attributes
        response.Item.Should().NotContainKey("Id");
        response.Item.Should().NotContainKey("Status");
        response.Item.Should().NotContainKey("Total");
    }

    /// <summary>
    /// Test 5: BatchGetItemRequest with per-table projections.
    /// </summary>
    [Fact]
    public async Task BatchGetItemRequest_WithProjection_ReturnsProjectedAttributes()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var item1 = AttributeValueFixtures.CreateTestEntityItem(
            id: id1,
            name: "Batch Item 1",
            count: 10);

        var item2 = AttributeValueFixtures.CreateTestEntityItem(
            id: id2,
            name: "Batch Item 2",
            count: 20);

        await _client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });

        // Act - BatchGetItem with projection
        var batchRequest = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                [_tableName] = new KeysAndAttributes
                {
                    Keys = new List<Dictionary<string, AttributeValue>>
                    {
                        new() { ["Id"] = new AttributeValue { S = id1.ToString() } },
                        new() { ["Id"] = new AttributeValue { S = id2.ToString() } }
                    }
                }
            }
        }.WithProjection(_tableName, _projectionBuilder, (TestIntegrationEntity p) => new { p.Name, p.Count });

        var response = await _client.BatchGetItemAsync(batchRequest);

        // Assert
        response.Responses.Should().ContainKey(_tableName);
        var items = response.Responses[_tableName];

        items.Should().HaveCount(2);

        foreach (var returnedItem in items)
        {
            // Should only contain projected attributes
            returnedItem.Should().ContainKey("Name");
            returnedItem.Should().ContainKey("Count");

            // Should NOT contain Id or other attributes
            returnedItem.Should().NotContainKey("Id");
            returnedItem.Should().NotContainKey("Status");
            returnedItem.Should().NotContainKey("Enabled");
        }

        // Verify specific values
        items.Should().Contain(i => i["Name"].S == "Batch Item 1" && i["Count"].N == "10");
        items.Should().Contain(i => i["Name"].S == "Batch Item 2" && i["Count"].N == "20");
    }
}
