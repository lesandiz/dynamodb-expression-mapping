using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Testcontainers.DynamoDb;
using Xunit;

namespace DynamoDb.ExpressionMapping.IntegrationTests.Integration;

/// <summary>
/// Shared fixture that starts a DynamoDB Local container once per test collection.
/// Implements IAsyncLifetime for xUnit's collection fixture pattern.
/// </summary>
public class DynamoDbFixture : IAsyncLifetime
{
    private DynamoDbContainer? _container;
    public IAmazonDynamoDB Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new DynamoDbBuilder()
            .WithImage("amazon/dynamodb-local:latest")
            .Build();

        await _container.StartAsync();

        Client = new AmazonDynamoDBClient(
            new BasicAWSCredentials("test", "test"),
            new AmazonDynamoDBConfig { ServiceURL = _container.GetConnectionString() });
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Helper to create a table with specified key schema.
    /// </summary>
    public async Task<string> CreateTableAsync(
        string tableName,
        string partitionKeyName,
        ScalarAttributeType partitionKeyType,
        string? sortKeyName = null,
        ScalarAttributeType? sortKeyType = null)
    {
        var keySchema = new List<KeySchemaElement>
        {
            new() { AttributeName = partitionKeyName, KeyType = KeyType.HASH }
        };

        var attributeDefinitions = new List<AttributeDefinition>
        {
            new() { AttributeName = partitionKeyName, AttributeType = partitionKeyType }
        };

        if (sortKeyName != null && sortKeyType != null)
        {
            keySchema.Add(new KeySchemaElement { AttributeName = sortKeyName, KeyType = KeyType.RANGE });
            attributeDefinitions.Add(new AttributeDefinition { AttributeName = sortKeyName, AttributeType = sortKeyType.Value });
        }

        await Client.CreateTableAsync(new CreateTableRequest
        {
            TableName = tableName,
            KeySchema = keySchema,
            AttributeDefinitions = attributeDefinitions,
            BillingMode = BillingMode.PAY_PER_REQUEST
        });

        return tableName;
    }

    /// <summary>
    /// Helper to delete a table.
    /// </summary>
    public async Task DeleteTableAsync(string tableName)
    {
        try
        {
            await Client.DeleteTableAsync(tableName);
        }
        catch (ResourceNotFoundException)
        {
            // Table already deleted
        }
    }
}

[CollectionDefinition("DynamoDb")]
public class DynamoDbCollection : ICollectionFixture<DynamoDbFixture> { }
