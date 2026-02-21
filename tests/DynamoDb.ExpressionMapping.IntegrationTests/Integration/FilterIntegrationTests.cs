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
/// Integration tests for FilterExpressionBuilder verifying that generated expressions
/// are accepted by DynamoDB and return the expected results.
/// </summary>
[Collection("DynamoDb")]
[Trait("Category", "Integration")]
public class FilterIntegrationTests : IAsyncLifetime
{
    private readonly DynamoDbFixture _fixture;
    private readonly string _tableName;
    private FilterExpressionBuilder<TestIntegrationEntity> _filterBuilder = null!;

    public FilterIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
        _tableName = $"FilterTests_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        await _fixture.CreateTableAsync(_tableName, "Id", ScalarAttributeType.S);

        // Initialize filter builder with default configuration
        var resolverFactory = new AttributeNameResolverFactory();
        var converterRegistry = AttributeValueConverterRegistry.Default;
        _filterBuilder = new FilterExpressionBuilder<TestIntegrationEntity>(resolverFactory, converterRegistry);
    }

    public async Task DisposeAsync()
    {
        await _fixture.DeleteTableAsync(_tableName);
    }

    /// <summary>
    /// Test 1: ComplexBooleanFilter_CorrectParentheses_ReturnsMatchingItems
    /// Verifies complex boolean expressions with parentheses work correctly: (A AND B) OR (NOT C)
    /// </summary>
    [Fact]
    public async Task ComplexBooleanFilter_CorrectParentheses_ReturnsMatchingItems()
    {
        // Arrange: Create test items with different combinations
        var item1 = AttributeValueFixtures.CreateTestEntityItem(
            name: "Test1",
            count: 10,
            enabled: true);
        var item2 = AttributeValueFixtures.CreateTestEntityItem(
            name: "Test2",
            count: 5,
            enabled: false);
        var item3 = AttributeValueFixtures.CreateTestEntityItem(
            name: "Test3",
            count: 15,
            enabled: false);
        var item4 = AttributeValueFixtures.CreateTestEntityItem(
            name: "Test4",
            count: 3,
            enabled: true);

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item1
        });
        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item2
        });
        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item3
        });
        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item4
        });

        // Act: Apply complex filter: (Count > 5 AND Enabled) OR (!Enabled)
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => (e.Count > 5 && e.Enabled) || !e.Enabled);

        var response = await _fixture.Client.ScanAsync(scanRequest);

        // Assert: Should match item1 (10 > 5 AND true), item2 (NOT enabled), item3 (NOT enabled)
        // Should NOT match item4 (3 <= 5 AND enabled is true, so first part false, and enabled is true so second part false)
        response.Items.Should().HaveCount(3);
        response.Items.Should().Contain(i => i["Name"].S == "Test1");
        response.Items.Should().Contain(i => i["Name"].S == "Test2");
        response.Items.Should().Contain(i => i["Name"].S == "Test3");
        response.Items.Should().NotContain(i => i["Name"].S == "Test4");
    }

    /// <summary>
    /// Test 2: StringFunctions_AcceptedByDynamoDB
    /// Verifies DynamoDB string functions (begins_with, contains, BETWEEN) work correctly
    /// </summary>
    [Fact]
    public async Task StringFunctions_AcceptedByDynamoDB()
    {
        // Arrange
        var item1 = AttributeValueFixtures.CreateTestEntityItem(name: "Alpha", count: 5);
        var item2 = AttributeValueFixtures.CreateTestEntityItem(name: "Beta", count: 10);
        var item3 = AttributeValueFixtures.CreateTestEntityItem(name: "Alphabet", count: 7);

        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item3 });

        // Act & Assert: Test begins_with
        var beginsWithRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => e.Name.StartsWith("Alp"));
        var beginsWithResponse = await _fixture.Client.ScanAsync(beginsWithRequest);
        beginsWithResponse.Items.Should().HaveCount(2);
        beginsWithResponse.Items.Should().Contain(i => i["Name"].S == "Alpha");
        beginsWithResponse.Items.Should().Contain(i => i["Name"].S == "Alphabet");

        // Act & Assert: Test contains
        var containsRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => e.Name.Contains("eta"));
        var containsResponse = await _fixture.Client.ScanAsync(containsRequest);
        containsResponse.Items.Should().HaveCount(1);
        containsResponse.Items[0]["Name"].S.Should().Be("Beta");

        // Act & Assert: Test BETWEEN
        var betweenRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => DynamoDbFunctions.Between(e.Count, 6, 10));
        var betweenResponse = await _fixture.Client.ScanAsync(betweenRequest);
        betweenResponse.Items.Should().HaveCount(2);
        betweenResponse.Items.Should().Contain(i => i["Name"].S == "Beta");
        betweenResponse.Items.Should().Contain(i => i["Name"].S == "Alphabet");
    }

    /// <summary>
    /// Test 3: NullCheck_GeneratesAttributeNotExists_FiltersCorrectly
    /// Verifies null checks generate attribute_not_exists and filter correctly
    /// </summary>
    [Fact]
    public async Task NullCheck_GeneratesAttributeNotExists_FiltersCorrectly()
    {
        // Arrange
        var item1 = AttributeValueFixtures.CreateTestEntityItem(name: "Item1", optionalScore: 100);
        var item2 = AttributeValueFixtures.CreateTestEntityItem(name: "Item2", optionalScore: null);
        var item3 = AttributeValueFixtures.CreateTestEntityItem(name: "Item3", optionalScore: 200);

        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item3 });

        // Act: Filter for items where OptionalScore is null (does not exist)
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => e.OptionalScore == null);

        var response = await _fixture.Client.ScanAsync(scanRequest);

        // Assert: Should only match item2
        response.Items.Should().HaveCount(1);
        response.Items[0]["Name"].S.Should().Be("Item2");

        // Act: Filter for items where OptionalScore is NOT null (exists)
        var notNullRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => e.OptionalScore != null);

        var notNullResponse = await _fixture.Client.ScanAsync(notNullRequest);

        // Assert: Should match item1 and item3
        notNullResponse.Items.Should().HaveCount(2);
        notNullResponse.Items.Should().Contain(i => i["Name"].S == "Item1");
        notNullResponse.Items.Should().Contain(i => i["Name"].S == "Item3");
    }

    /// <summary>
    /// Test 4: InOperator_FiltersToMatchingValues
    /// Verifies IN operator with multiple values works correctly
    /// </summary>
    [Fact]
    public async Task InOperator_FiltersToMatchingValues()
    {
        // Arrange
        var item1 = AttributeValueFixtures.CreateTestEntityItem(name: "Item1", status: TestStatus.Active);
        var item2 = AttributeValueFixtures.CreateTestEntityItem(name: "Item2", status: TestStatus.Inactive);
        var item3 = AttributeValueFixtures.CreateTestEntityItem(name: "Item3", status: TestStatus.Suspended);
        var item4 = AttributeValueFixtures.CreateTestEntityItem(name: "Item4", status: TestStatus.Active);

        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item3 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item4 });

        // Act: Filter for items with Status IN (Active, Suspended)
        var statusArray = new[] { TestStatus.Active, TestStatus.Suspended };
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => statusArray.Contains(e.Status));

        var response = await _fixture.Client.ScanAsync(scanRequest);

        // Assert: Should match item1, item3, item4
        response.Items.Should().HaveCount(3);
        response.Items.Should().Contain(i => i["Name"].S == "Item1");
        response.Items.Should().Contain(i => i["Name"].S == "Item3");
        response.Items.Should().Contain(i => i["Name"].S == "Item4");
        response.Items.Should().NotContain(i => i["Name"].S == "Item2");
    }

    /// <summary>
    /// Test 5: ComposedAndFilter_ReAliasedExpression_ReturnsMatchingItems
    /// Verifies that composed filters using FilterExpressionResult.And() with re-aliasing work correctly
    /// </summary>
    [Fact]
    public async Task ComposedAndFilter_ReAliasedExpression_ReturnsMatchingItems()
    {
        // Arrange
        var item1 = AttributeValueFixtures.CreateTestEntityItem(name: "Item1", count: 10, enabled: true);
        var item2 = AttributeValueFixtures.CreateTestEntityItem(name: "Item2", count: 20, enabled: false);
        var item3 = AttributeValueFixtures.CreateTestEntityItem(name: "Item3", count: 15, enabled: true);
        var item4 = AttributeValueFixtures.CreateTestEntityItem(name: "Item4", count: 5, enabled: true);

        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item3 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item4 });

        // Act: Build two separate filters and compose them
        var filter1 = _filterBuilder.BuildFilter(e => e.Count > 8);
        var filter2 = _filterBuilder.BuildFilter(e => e.Enabled);
        var composedFilter = FilterExpressionResult.And(filter1, filter2);

        var scanRequest = new ScanRequest { TableName = _tableName }
            .ApplyFilter(composedFilter);

        var response = await _fixture.Client.ScanAsync(scanRequest);

        // Assert: Should match item1 (count=10, enabled=true) and item3 (count=15, enabled=true)
        // Should NOT match item2 (enabled=false) or item4 (count=5)
        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(i => i["Name"].S == "Item1");
        response.Items.Should().Contain(i => i["Name"].S == "Item3");
    }

    /// <summary>
    /// Test 6: EnumFilter_MatchesStoredEnumValue
    /// Verifies enum value filters round-trip correctly
    /// </summary>
    [Fact]
    public async Task EnumFilter_MatchesStoredEnumValue()
    {
        // Arrange
        var item1 = AttributeValueFixtures.CreateTestEntityItem(name: "Item1", status: TestStatus.Active);
        var item2 = AttributeValueFixtures.CreateTestEntityItem(name: "Item2", status: TestStatus.Inactive);
        var item3 = AttributeValueFixtures.CreateTestEntityItem(name: "Item3", status: TestStatus.Active);

        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item3 });

        // Act: Filter for items with Status == Active
        var activeStatus = TestStatus.Active;
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => e.Status == activeStatus);

        var response = await _fixture.Client.ScanAsync(scanRequest);

        // Assert: Should match item1 and item3
        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(i => i["Name"].S == "Item1");
        response.Items.Should().Contain(i => i["Name"].S == "Item3");
        response.Items.Should().NotContain(i => i["Name"].S == "Item2");
    }

    /// <summary>
    /// Test 7: NullableAttribute_FilterOnExistence_FiltersCorrectly
    /// Verifies nullable attribute filtering using attribute_exists function
    /// </summary>
    [Fact]
    public async Task NullableAttribute_FilterOnExistence_FiltersCorrectly()
    {
        // Arrange
        var item1 = AttributeValueFixtures.CreateTestEntityItem(
            name: "Item1",
            expiresOn: DateTime.UtcNow.AddDays(10));
        var item2 = AttributeValueFixtures.CreateTestEntityItem(
            name: "Item2",
            expiresOn: null);
        var item3 = AttributeValueFixtures.CreateTestEntityItem(
            name: "Item3",
            expiresOn: DateTime.UtcNow.AddDays(5));

        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item1 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item2 });
        await _fixture.Client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = item3 });

        // Act: Filter for items where ExpiresOn attribute exists
        var scanRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => DynamoDbFunctions.AttributeExists(e.ExpiresOn));

        var response = await _fixture.Client.ScanAsync(scanRequest);

        // Assert: Should match item1 and item3 (both have ExpiresOn attribute)
        response.Items.Should().HaveCount(2);
        response.Items.Should().Contain(i => i["Name"].S == "Item1");
        response.Items.Should().Contain(i => i["Name"].S == "Item3");
        response.Items.Should().NotContain(i => i["Name"].S == "Item2");

        // Act: Filter for items where ExpiresOn attribute does not exist
        var notExistsRequest = new ScanRequest { TableName = _tableName }
            .WithFilter(_filterBuilder, e => DynamoDbFunctions.AttributeNotExists(e.ExpiresOn));

        var notExistsResponse = await _fixture.Client.ScanAsync(notExistsRequest);

        // Assert: Should only match item2
        notExistsResponse.Items.Should().HaveCount(1);
        notExistsResponse.Items[0]["Name"].S.Should().Be("Item2");
    }
}
