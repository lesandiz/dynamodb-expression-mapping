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
/// Integration tests for KeyConditionExpressionBuilder verifying that generated expressions
/// are accepted by DynamoDB and return the expected results.
/// </summary>
[Collection("DynamoDb")]
[Trait("Category", "Integration")]
public class KeyConditionIntegrationTests : IAsyncLifetime
{
    private readonly DynamoDbFixture _fixture;
    private readonly string _tableName;
    private readonly KeyConditionExpressionBuilder<TestKeyedEntity> _builder;

    public KeyConditionIntegrationTests(DynamoDbFixture fixture)
    {
        _fixture = fixture;
        _tableName = $"KeyConditionTests_{Guid.NewGuid():N}";

        var factory = new AttributeNameResolverFactory();
        var converters = AttributeValueConverterRegistry.Default;
        _builder = new KeyConditionExpressionBuilder<TestKeyedEntity>(factory, converters);
    }

    public async Task InitializeAsync()
    {
        await _fixture.CreateTableAsync(
            _tableName,
            partitionKeyName: "PK",
            partitionKeyType: ScalarAttributeType.S,
            sortKeyName: "SK",
            sortKeyType: ScalarAttributeType.S);

        // Seed test data
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DeleteTableAsync(_tableName);
    }

    private async Task SeedTestDataAsync()
    {
        // Create items with partition key "USER#123" and various sort keys
        var items = new[]
        {
            AttributeValueFixtures.CreateKeyedEntityItem("USER#123", "ORDER#2024-01-15", "Order A", TestStatus.Active),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#123", "ORDER#2024-02-20", "Order B", TestStatus.Inactive),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#123", "ORDER#2024-03-10", "Order C", TestStatus.Active),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#123", "INVOICE#2024-01-20", "Invoice A", TestStatus.Active),
            AttributeValueFixtures.CreateKeyedEntityItem("USER#456", "ORDER#2024-01-10", "Other User Order", TestStatus.Active),
        };

        foreach (var item in items)
        {
            await _fixture.Client.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            });
        }
    }

    [Fact]
    public async Task PartitionKeyOnly_ReturnsMatchingItems()
    {
        // Arrange
        var request = new QueryRequest { TableName = _tableName }
            .WithKeyCondition(_builder, b => b.WithPartitionKey(e => e.PK, "USER#123").Build());

        // Act
        var response = await _fixture.Client.QueryAsync(request);

        // Assert
        response.Items.Should().HaveCount(4);
        response.Items.Should().OnlyContain(item => item["PK"].S == "USER#123");
        response.Items.Select(item => item["SK"].S).Should().Contain(new[]
        {
            "ORDER#2024-01-15",
            "ORDER#2024-02-20",
            "ORDER#2024-03-10",
            "INVOICE#2024-01-20"
        });
    }

    [Fact]
    public async Task SortKeyEquals_ReturnsSingleItem()
    {
        // Arrange
        var request = new QueryRequest { TableName = _tableName }
            .WithKeyCondition(_builder, b => b
                .WithPartitionKey(e => e.PK, "USER#123")
                .WithSortKeyEquals(e => e.SK, "ORDER#2024-02-20"));

        // Act
        var response = await _fixture.Client.QueryAsync(request);

        // Assert
        response.Items.Should().ContainSingle();
        response.Items[0]["SK"].S.Should().Be("ORDER#2024-02-20");
        response.Items[0]["Data"].S.Should().Be("Order B");
        response.Items[0]["Status"].S.Should().Be("Inactive");
    }

    [Fact]
    public async Task SortKeyBeginsWith_ReturnsMatchingItems()
    {
        // Arrange
        var request = new QueryRequest { TableName = _tableName }
            .WithKeyCondition(_builder, b => b
                .WithPartitionKey(e => e.PK, "USER#123")
                .WithSortKeyBeginsWith(e => e.SK, "ORDER#"));

        // Act
        var response = await _fixture.Client.QueryAsync(request);

        // Assert
        response.Items.Should().HaveCount(3);
        response.Items.Should().OnlyContain(item =>
            item["PK"].S == "USER#123" && item["SK"].S.StartsWith("ORDER#"));
        response.Items.Select(item => item["SK"].S).Should().BeEquivalentTo(new[]
        {
            "ORDER#2024-01-15",
            "ORDER#2024-02-20",
            "ORDER#2024-03-10"
        });
    }

    [Fact]
    public async Task SortKeyBetween_ReturnsItemsInRange()
    {
        // Arrange
        var request = new QueryRequest { TableName = _tableName }
            .WithKeyCondition(_builder, b => b
                .WithPartitionKey(e => e.PK, "USER#123")
                .WithSortKeyBetween(e => e.SK, "ORDER#2024-01-01", "ORDER#2024-02-28"));

        // Act
        var response = await _fixture.Client.QueryAsync(request);

        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Select(item => item["SK"].S).Should().BeEquivalentTo(new[]
        {
            "ORDER#2024-01-15",
            "ORDER#2024-02-20"
        });
    }

    [Fact]
    public async Task SortKeyGreaterThan_ReturnsItemsAfterValue()
    {
        // Arrange
        var request = new QueryRequest { TableName = _tableName }
            .WithKeyCondition(_builder, b => b
                .WithPartitionKey(e => e.PK, "USER#123")
                .WithSortKeyGreaterThan(e => e.SK, "ORDER#2024-02-01"));

        // Act
        var response = await _fixture.Client.QueryAsync(request);

        // Assert
        response.Items.Should().HaveCount(2);
        response.Items.Select(item => item["SK"].S).Should().BeEquivalentTo(new[]
        {
            "ORDER#2024-02-20",
            "ORDER#2024-03-10"
        });
    }

    [Fact]
    public async Task ReservedKeywordKeyAttribute_AliasedAndAccepted()
    {
        // Arrange - Create a test entity with "Status" as the sort key (reserved keyword)
        var testTableName = $"KeyConditionReservedTests_{Guid.NewGuid():N}";

        try
        {
            // Create table with "Status" (reserved keyword) as sort key
            await _fixture.CreateTableAsync(
                testTableName,
                partitionKeyName: "PK",
                partitionKeyType: ScalarAttributeType.S,
                sortKeyName: "Status",
                sortKeyType: ScalarAttributeType.S);

            // Seed data
            await _fixture.Client.PutItemAsync(new PutItemRequest
            {
                TableName = testTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = "ENTITY#1" },
                    ["Status"] = new() { S = "Active" },
                    ["Data"] = new() { S = "Test Data" }
                }
            });

            await _fixture.Client.PutItemAsync(new PutItemRequest
            {
                TableName = testTableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new() { S = "ENTITY#1" },
                    ["Status"] = new() { S = "Inactive" },
                    ["Data"] = new() { S = "Other Data" }
                }
            });

            // Build key condition with reserved keyword "Status"
            var request = new QueryRequest { TableName = testTableName }
                .WithKeyCondition(_builder, b => b
                    .WithPartitionKey(e => e.PK, "ENTITY#1")
                    .WithSortKeyEquals(e => e.Status, TestStatus.Active));

            // Act
            var response = await _fixture.Client.QueryAsync(request);

            // Assert
            response.Items.Should().ContainSingle();
            response.Items[0]["Status"].S.Should().Be("Active");
            response.Items[0]["Data"].S.Should().Be("Test Data");

            // Verify that aliases were used (check the request properties)
            request.ExpressionAttributeNames.Should().NotBeNull();
            request.ExpressionAttributeNames.Should().ContainValue("Status");
            request.KeyConditionExpression.Should().Contain("#key_");
        }
        finally
        {
            await _fixture.DeleteTableAsync(testTableName);
        }
    }
}
