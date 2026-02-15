using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;
using DynamoDb.ExpressionMapping.ResultMapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Integration;

/// <summary>
/// Integration tests for DirectResultMapper with DynamoDB.
/// Verifies the full pipeline: write item → query with projection → map to DTO.
/// </summary>
[Collection("DynamoDb")]
[Trait("Category", "Integration")]
public class DirectResultMapperIntegrationTests : IAsyncLifetime
{
    private readonly DynamoDbFixture _fixture;
    private readonly string _tableName;
    private readonly IAmazonDynamoDB _client;
    private readonly ProjectionBuilder<TestIntegrationEntity> _projectionBuilder;

    public DirectResultMapperIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
        _tableName = $"DirectResultMapperTests_{Guid.NewGuid():N}";

        // Initialize builder
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        _projectionBuilder = new ProjectionBuilder<TestIntegrationEntity>(
            resolverFactory,
            ReservedKeywordRegistry.Default,
            NullExpressionCache.Instance);
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
    /// Test 1: Projection + direct mapping pipeline with anonymous type.
    /// Write item → query with projection → map to anonymous DTO.
    /// </summary>
    [Fact]
    public async Task ProjectAndMap_AnonymousType_MapsFromRealResponse()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Anonymous Test",
            count: 42,
            enabled: true);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Project and map using anonymous type
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new { p.Name, p.Count, p.Enabled });

        var response = await _client.ScanAsync(scanRequest);

        // Create mapper and map results
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var resultMapper = new DirectResultMapper<TestIntegrationEntity>(
            resolverFactory,
            AttributeValueConverterRegistry.Default);
        var mapper = resultMapper.CreateMapper<object>(
            e => new { e.Name, e.Count, e.Enabled });

        var results = response.Items.Select(mapper).ToList();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];

        result.Should().NotBeNull();
        var anonymousType = result.GetType();
        var nameProperty = anonymousType.GetProperty("Name");
        var countProperty = anonymousType.GetProperty("Count");
        var enabledProperty = anonymousType.GetProperty("Enabled");

        nameProperty!.GetValue(result).Should().Be("Anonymous Test");
        countProperty!.GetValue(result).Should().Be(42);
        enabledProperty!.GetValue(result).Should().Be(true);
    }

    /// <summary>
    /// Test 2: Projection + direct mapping for named DTO type.
    /// Write item → query with projection → map to named type.
    /// </summary>
    [Fact]
    public async Task ProjectAndMap_NamedType_MapsFromRealResponse()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Named Type Test",
            count: 100,
            total: 299.99m);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Project and map using named type
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new { p.Name, p.Count, p.Total });

        var response = await _client.ScanAsync(scanRequest);

        // Create mapper and map results
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var resultMapper = new DirectResultMapper<TestIntegrationEntity>(
            resolverFactory,
            AttributeValueConverterRegistry.Default);
        var mapper = resultMapper.CreateMapper(
            e => new IntegrationDto
            {
                DisplayName = e.Name,
                ItemCount = e.Count,
                TotalAmount = e.Total
            });

        var results = response.Items.Select(mapper).ToList();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];

        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Named Type Test");
        result.ItemCount.Should().Be(100);
        result.TotalAmount.Should().Be(299.99m);
    }

    /// <summary>
    /// Test 3: Nested attribute projected and mapped.
    /// Write item with nested address → query with projection → map nested path.
    /// </summary>
    [Fact]
    public async Task ProjectAndMap_NestedAttribute_MapsFromRealResponse()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateNestedEntityItem(
            id: testId,
            city: "Edinburgh",
            postCode: "EH1 1YZ",
            floor: 7);

        // Add Name and Count for the mapper
        item["Name"] = new AttributeValue { S = "Nested Test" };
        item["Count"] = new AttributeValue { N = "50" };

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Project nested attributes
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new
            {
                p.Name,
                p.Count,
                City = p.Address!.City,
                PostCode = p.Address!.PostCode,
                Floor = p.Address!.Floor
            });

        var response = await _client.ScanAsync(scanRequest);

        // Create mapper for nested attributes
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var resultMapper = new DirectResultMapper<TestIntegrationEntity>(
            resolverFactory,
            AttributeValueConverterRegistry.Default);
        var mapper = resultMapper.CreateMapper(
            e => new
            {
                e.Name,
                e.Count,
                City = e.Address!.City,
                PostCode = e.Address!.PostCode,
                Floor = e.Address!.Floor
            });

        var results = response.Items.Select(mapper).ToList();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];

        result.Should().NotBeNull();
        result.Name.Should().Be("Nested Test");
        result.Count.Should().Be(50);
        result.City.Should().Be("Edinburgh");
        result.PostCode.Should().Be("EH1 1YZ");
        result.Floor.Should().Be(7);
    }

    /// <summary>
    /// Test 4: Enum attribute round-trips through projection and mapping.
    /// Write item with enum → query with projection → map enum correctly.
    /// </summary>
    [Fact]
    public async Task ProjectAndMap_EnumAttribute_RoundTripsCorrectly()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "Enum Test",
            count: 75,
            status: TestStatus.Suspended);

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });

        // Act - Project enum attribute
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new { p.Name, p.Count, p.Status });

        var response = await _client.ScanAsync(scanRequest);

        // Create mapper for enum
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var resultMapper = new DirectResultMapper<TestIntegrationEntity>(
            resolverFactory,
            AttributeValueConverterRegistry.Default);
        var mapper = resultMapper.CreateMapper(
            e => new
            {
                e.Name,
                e.Count,
                e.Status
            });

        var results = response.Items.Select(mapper).ToList();

        // Assert
        results.Should().HaveCount(1);
        var result = results[0];

        result.Should().NotBeNull();
        result.Name.Should().Be("Enum Test");
        result.Count.Should().Be(75);
        result.Status.Should().Be(TestStatus.Suspended);
    }

    /// <summary>
    /// Test 5: Nullable attribute projected when present and absent.
    /// Write two items: one with nullable value, one without → verify both map correctly.
    /// </summary>
    [Fact]
    public async Task ProjectAndMap_NullableAttribute_HandlesPresenceAndAbsence()
    {
        // Arrange - item WITH nullable value
        var testId1 = Guid.NewGuid();
        var item1 = AttributeValueFixtures.CreateTestEntityItem(
            id: testId1,
            name: "With Optional Score",
            count: 10,
            optionalScore: 95);

        // Arrange - item WITHOUT nullable value
        var testId2 = Guid.NewGuid();
        var item2 = AttributeValueFixtures.CreateTestEntityItem(
            id: testId2,
            name: "Without Optional Score",
            count: 20,
            optionalScore: null);

        await _client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });

        // Act - Project nullable attribute
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithProjection(_projectionBuilder, (TestIntegrationEntity p) => new
            {
                p.Name,
                p.Count,
                p.OptionalScore
            });

        var response = await _client.ScanAsync(scanRequest);

        // Create mapper for nullable
        var resolverFactory = new AttributeNameResolverFactoryBuilder().Build();
        var resultMapper = new DirectResultMapper<TestIntegrationEntity>(
            resolverFactory,
            AttributeValueConverterRegistry.Default);
        var mapper = resultMapper.CreateMapper(
            e => new
            {
                e.Name,
                e.Count,
                e.OptionalScore
            });

        var results = response.Items.Select(mapper).OrderBy(r => r.Count).ToList();

        // Assert
        results.Should().HaveCount(2);

        // First item - with nullable value
        var result1 = results[0];
        result1.Should().NotBeNull();
        result1.Name.Should().Be("With Optional Score");
        result1.Count.Should().Be(10);
        result1.OptionalScore.Should().Be(95);

        // Second item - without nullable value
        var result2 = results[1];
        result2.Should().NotBeNull();
        result2.Name.Should().Be("Without Optional Score");
        result2.Count.Should().Be(20);
        result2.OptionalScore.Should().BeNull();
    }

    #region Test Helper Types

    /// <summary>
    /// Named DTO for testing property setter mapping.
    /// </summary>
    private class IntegrationDto
    {
        public string DisplayName { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    #endregion
}
