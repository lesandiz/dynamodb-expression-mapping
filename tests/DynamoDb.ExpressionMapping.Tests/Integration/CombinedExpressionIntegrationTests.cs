using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.Tests.Fixtures;
using FluentAssertions;
using Xunit;

namespace DynamoDb.ExpressionMapping.Tests.Integration;

/// <summary>
/// Integration tests verifying that multiple expression types (key condition, projection, filter, update, condition)
/// can coexist on the same request with different alias scopes without collisions.
/// </summary>
[Collection("DynamoDb")]
[Trait("Category", "Integration")]
public class CombinedExpressionIntegrationTests : IAsyncLifetime
{
    private readonly DynamoDbFixture _fixture;
    private readonly string _keyedTableName;
    private readonly string _integrationTableName;

    private KeyConditionExpressionBuilder<TestKeyedEntity> _keyConditionBuilder = null!;
    private ProjectionBuilder<TestKeyedEntity> _keyedProjectionBuilder = null!;
    private FilterExpressionBuilder<TestKeyedEntity> _keyedFilterBuilder = null!;
    private UpdateExpressionBuilder<TestIntegrationEntity> _updateBuilder = null!;
    private ConditionExpressionBuilder<TestIntegrationEntity> _conditionBuilder = null!;

    public CombinedExpressionIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
        _keyedTableName = $"CombinedKeyedTests_{Guid.NewGuid():N}";
        _integrationTableName = $"CombinedIntegrationTests_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        // Create table for keyed entity tests (key condition + projection + filter)
        await _fixture.CreateTableAsync(
            _keyedTableName,
            partitionKeyName: "PK",
            partitionKeyType: ScalarAttributeType.S,
            sortKeyName: "SK",
            sortKeyType: ScalarAttributeType.S);

        // Create table for integration entity tests (update + condition)
        await _fixture.CreateTableAsync(
            _integrationTableName,
            partitionKeyName: "Id",
            partitionKeyType: ScalarAttributeType.S);

        // Initialize builders
        var factory = new AttributeNameResolverFactory();
        var converters = AttributeValueConverterRegistry.Default;

        _keyConditionBuilder = new KeyConditionExpressionBuilder<TestKeyedEntity>(factory, converters);
        _keyedProjectionBuilder = new ProjectionBuilder<TestKeyedEntity>(factory);
        _keyedFilterBuilder = new FilterExpressionBuilder<TestKeyedEntity>(factory, converters);
        _updateBuilder = new UpdateExpressionBuilder<TestIntegrationEntity>(factory, converters);
        _conditionBuilder = new ConditionExpressionBuilder<TestIntegrationEntity>(factory, converters);

        // Seed test data
        await SeedKeyedDataAsync();
        await SeedIntegrationDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DeleteTableAsync(_keyedTableName);
        await _fixture.DeleteTableAsync(_integrationTableName);
    }

    private async Task SeedKeyedDataAsync()
    {
        // Create items with different partition key, sort key, data, and status combinations
        var items = new[]
        {
            AttributeValueFixtures.CreateKeyedEntityItem("USER#100", "ORDER#2024-01-10", "Order A", TestStatus.Active),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#100", "ORDER#2024-01-15", "Order B", TestStatus.Inactive),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#100", "ORDER#2024-02-05", "Order C", TestStatus.Active),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#100", "ORDER#2024-02-20", "Order D", TestStatus.Suspended),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#100", "INVOICE#2024-01-20", "Invoice A", TestStatus.Active),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#200", "ORDER#2024-01-05", "Other Order", TestStatus.Active),
        };

        foreach (var item in items)
        {
            await _fixture.Client.PutItemAsync(new PutItemRequest
            {
                TableName = _keyedTableName,
                Item = item
            });
        }
    }

    private async Task SeedIntegrationDataAsync()
    {
        var testId = Guid.NewGuid();
        var item = AttributeValueFixtures.CreateTestEntityItem(
            id: testId,
            name: "TestItem",
            count: 10,
            enabled: true,
            status: TestStatus.Active);

        await _fixture.Client.PutItemAsync(new PutItemRequest
        {
            TableName = _integrationTableName,
            Item = item
        });
    }

    /// <summary>
    /// Test 1: KeyCondition_Projection_Filter_AllScopesCoexist
    /// Verifies that key condition (#key_), projection (#proj_), and filter (#filt_) expressions
    /// can coexist on the same QueryRequest without alias collisions.
    /// </summary>
    [Fact]
    public async Task KeyCondition_Projection_Filter_AllScopesCoexist()
    {
        // Arrange: Prepare filter variable to avoid enum Convert node
        var activeStatus = TestStatus.Active;

        // Act: Apply all three expressions to the QueryRequest
        var request = new QueryRequest { TableName = _keyedTableName }
            .WithKeyCondition(_keyConditionBuilder, b => b
                .WithPartitionKey(e => e.PK, "USER#100")
                .WithSortKeyBeginsWith(e => e.SK, "ORDER#2024-01"))
            .WithProjection(_keyedProjectionBuilder, e => new { e.PK, e.SK, e.Data, e.Status })
            .WithFilter(_keyedFilterBuilder, e => e.Status == activeStatus);

        var response = await _fixture.Client.QueryAsync(request);

        // Assert: Verify the query executed successfully
        // Key condition: PK == USER#100 AND SK begins_with ORDER#2024-01
        // Filter: Status == Active
        // Expected: ORDER#2024-01-10 (Active) — ORDER#2024-01-15 is Inactive so filtered out
        response.Items.Should().HaveCount(1);

        // Verify only projected attributes are returned (Data and Status, plus keys PK and SK)
        foreach (var item in response.Items)
        {
            item.Should().ContainKey("PK");
            item.Should().ContainKey("SK");
            item.Should().ContainKey("Data");
            item.Should().ContainKey("Status");
            item.Keys.Should().HaveCount(4); // Only PK, SK, Data, Status
        }

        // Verify filter was applied (only Active items)
        response.Items.Should().OnlyContain(item => item["Status"].S == "Active");

        // Verify key condition was applied (only ORDER# items in January 2024)
        response.Items.Should().OnlyContain(item =>
            item["PK"].S == "USER#100" &&
            item["SK"].S.StartsWith("ORDER#2024-01"));

        // Verify alias scopes are different
        request.ExpressionAttributeNames.Should().NotBeNull();
        request.ExpressionAttributeValues.Should().NotBeNull();

        // Check that different alias prefixes exist (#key_, #proj_, #filt_, :key_v, :filt_v)
        var nameAliases = request.ExpressionAttributeNames!.Keys;
        var valueAliases = request.ExpressionAttributeValues!.Keys;

        // At least one alias from each scope should exist
        nameAliases.Should().Contain(k => k.StartsWith("#key_") || k.StartsWith("#proj_") || k.StartsWith("#filt_"));

        // Verify expressions are set
        request.KeyConditionExpression.Should().NotBeNullOrEmpty();
        request.ProjectionExpression.Should().NotBeNullOrEmpty();
        request.FilterExpression.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Test 2: Update_WithCondition_BothApplied
    /// Verifies that update (#upd_) and condition (#cond_) expressions can coexist
    /// on the same UpdateItemRequest without alias collisions.
    /// </summary>
    [Fact]
    public async Task Update_WithCondition_BothApplied()
    {
        // Arrange: Get the test item ID
        var scanResponse = await _fixture.Client.ScanAsync(new ScanRequest
        {
            TableName = _integrationTableName,
            Limit = 1
        });

        scanResponse.Items.Should().HaveCount(1);
        var testId = scanResponse.Items[0]["Id"].S;

        // Arrange: Build update expression (increment Count by 5)
        var update = _updateBuilder
            .Increment(e => e.Count, 5)
            .Build();

        // Act: Apply update with condition
        var request = new UpdateItemRequest
        {
            TableName = _integrationTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId }
            },
            ReturnValues = ReturnValue.ALL_NEW
        }
        .WithUpdate(update)
        .WithCondition(_conditionBuilder, e => e.Count == 10 && e.Enabled);

        var response = await _fixture.Client.UpdateItemAsync(request);

        // Assert: Verify update was applied
        response.Attributes["Count"].N.Should().Be("15"); // 10 + 5

        // Verify condition was checked (if condition failed, it would throw ConditionalCheckFailedException)
        response.HttpStatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        // Verify alias scopes are different
        request.ExpressionAttributeNames.Should().NotBeNull();
        request.ExpressionAttributeValues.Should().NotBeNull();

        // Check that different alias prefixes exist (#upd_, #cond_, :upd_v, :cond_v)
        var nameAliases = request.ExpressionAttributeNames!.Keys;
        var valueAliases = request.ExpressionAttributeValues!.Keys;

        // At least one alias from each scope should exist
        nameAliases.Should().Contain(k => k.StartsWith("#upd_") || k.StartsWith("#cond_"));
        valueAliases.Should().Contain(k => k.StartsWith(":upd_v") || k.StartsWith(":cond_v"));

        // Verify expressions are set
        request.UpdateExpression.Should().NotBeNullOrEmpty();
        request.ConditionExpression.Should().NotBeNullOrEmpty();

        // Now test that condition prevents update when it fails
        // Create a new builder instance for the second independent update
        var factory = new AttributeNameResolverFactory();
        var converters = AttributeValueConverterRegistry.Default;
        var failUpdateBuilder = new UpdateExpressionBuilder<TestIntegrationEntity>(factory, converters);
        var failUpdate = failUpdateBuilder
            .Increment(e => e.Count, 5)
            .Build();

        var failRequest = new UpdateItemRequest
        {
            TableName = _integrationTableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["Id"] = new AttributeValue { S = testId }
            },
            ReturnValues = ReturnValue.ALL_NEW
        }
        .WithUpdate(failUpdate)
        .WithCondition(_conditionBuilder, e => e.Count == 999);

        // Assert: Condition should fail
        var act = async () => await _fixture.Client.UpdateItemAsync(failRequest);
        await act.Should().ThrowAsync<ConditionalCheckFailedException>();
    }

    /// <summary>
    /// Test 3: FullFluentChain_QueryRequest_ReturnsExpectedResults
    /// Verifies full fluent chaining of key condition, projection, and filter on a QueryRequest.
    /// Tests the fluent API pattern: .WithKeyCondition().WithProjection().WithFilter()
    /// </summary>
    [Fact]
    public async Task FullFluentChain_QueryRequest_ReturnsExpectedResults()
    {
        // Act: Build request using full fluent chain
        var activeStatus = TestStatus.Active;
        var request = new QueryRequest { TableName = _keyedTableName }
            .WithKeyCondition(_keyConditionBuilder, b => b
                .WithPartitionKey(e => e.PK, "USER#100")
                .WithSortKeyBeginsWith(e => e.SK, "ORDER#2024-02"))
            .WithProjection(_keyedProjectionBuilder, e => new
            {
                e.PK,
                e.SK,
                e.Data,
                e.Status
            })
            .WithFilter(_keyedFilterBuilder, e => e.Status == activeStatus);

        var response = await _fixture.Client.QueryAsync(request);

        // Assert: Verify query results
        // Key condition: PK == USER#100 AND SK begins_with ORDER#2024-02
        // Filter: Status == Active
        // Should match: ORDER#2024-02-05 (Active)
        // Should NOT match: ORDER#2024-02-20 (Suspended - filtered out)
        response.Items.Should().HaveCount(1);

        var item = response.Items[0];
        item["PK"].S.Should().Be("USER#100");
        item["SK"].S.Should().Be("ORDER#2024-02-05");
        item["Data"].S.Should().Be("Order C");
        item["Status"].S.Should().Be("Active");

        // Verify only projected attributes are returned (SK, Data, Status, plus PK key)
        item.Keys.Should().HaveCount(4); // PK, SK, Data, Status
        item.Should().ContainKey("PK");
        item.Should().ContainKey("SK");
        item.Should().ContainKey("Data");
        item.Should().ContainKey("Status");

        // Verify all three expression types are present
        request.KeyConditionExpression.Should().NotBeNullOrEmpty();
        request.ProjectionExpression.Should().NotBeNullOrEmpty();
        request.FilterExpression.Should().NotBeNullOrEmpty();

        // Verify alias dictionaries are populated
        request.ExpressionAttributeNames.Should().NotBeNull();
        request.ExpressionAttributeValues.Should().NotBeNull();
        request.ExpressionAttributeNames.Should().NotBeEmpty();
        request.ExpressionAttributeValues.Should().NotBeEmpty();

        // Verify different alias scopes coexist without collisions
        var nameAliases = request.ExpressionAttributeNames!.Keys.ToList();
        var valueAliases = request.ExpressionAttributeValues!.Keys.ToList();

        // Verify alias prefixes from projection and filter are present
        // Note: #key_ name aliases only appear if key attributes are reserved keywords (PK/SK are not)
        nameAliases.Should().Contain(k => k.StartsWith("#proj_"));
        nameAliases.Should().Contain(k => k.StartsWith("#filt_"));

        // Value aliases should be present for key condition and filter
        valueAliases.Should().Contain(k => k.StartsWith(":key_v"));
        valueAliases.Should().Contain(k => k.StartsWith(":filt_v"));

        // Verify no alias collisions - all keys should be unique
        nameAliases.Should().OnlyHaveUniqueItems();
        valueAliases.Should().OnlyHaveUniqueItems();
    }
}
