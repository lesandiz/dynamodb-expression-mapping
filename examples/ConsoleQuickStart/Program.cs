using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ConsoleQuickStart;
using DynamoDb.ExpressionMapping;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;

// Create DynamoDB client pointing at local
var client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
{
    ServiceURL = "http://localhost:8002"
});

// Create table (idempotent)
await CreateTableAsync(client);

// Seed data
await SeedDataAsync(client);

Console.WriteLine("Setup complete. Table created and seeded.\n");

// 1. Build configuration with custom converter
var config = new DynamoDbExpressionConfig.Builder()
    .WithConverter(new MoneyConverter())
    .Build();

// 2. Create resolver factory (auto-discovers types via reflection)
var resolverFactory = new AttributeNameResolverFactory();

// 3. Create all builders manually
var projectionBuilder = new ProjectionBuilder<Order>(
    resolverFactory, config.ReservedKeywords, config.Cache);

var filterBuilder = new FilterExpressionBuilder<Order>(
    resolverFactory, config.ConverterRegistry);

var conditionBuilder = new ConditionExpressionBuilder<Order>(
    resolverFactory, config.ConverterRegistry);

var updateBuilder = new UpdateExpressionBuilder<Order>(
    resolverFactory, config.ConverterRegistry);

var keyConditionBuilder = new KeyConditionExpressionBuilder<Order>(
    resolverFactory, config.ConverterRegistry);

var resultMapper = new DirectResultMapper<Order>(
    resolverFactory, config.ConverterRegistry, config.Cache);

// Helper methods
static async Task CreateTableAsync(IAmazonDynamoDB client)
{
    try
    {
        await client.CreateTableAsync(new CreateTableRequest
        {
            TableName = "Orders",
            KeySchema = new List<KeySchemaElement>
            {
                new KeySchemaElement { AttributeName = "PK", KeyType = KeyType.HASH },
                new KeySchemaElement { AttributeName = "SK", KeyType = KeyType.RANGE }
            },
            AttributeDefinitions = new List<AttributeDefinition>
            {
                new AttributeDefinition { AttributeName = "PK", AttributeType = ScalarAttributeType.S },
                new AttributeDefinition { AttributeName = "SK", AttributeType = ScalarAttributeType.S }
            },
            BillingMode = BillingMode.PAY_PER_REQUEST
        });
        Console.WriteLine("Table 'Orders' created successfully.");
    }
    catch (ResourceInUseException)
    {
        Console.WriteLine("Table 'Orders' already exists.");
    }
}

static async Task SeedDataAsync(IAmazonDynamoDB client)
{
    var orders = new[]
    {
        new
        {
            PK = "CUSTOMER#alice",
            SK = "ORDER#001",
            Name = "Alice's Laptop",
            Status = "Shipped",
            Total = new { Amount = 1299.99m, Currency = "USD" },
            Quantity = 1,
            ShippingAddress = new { Street = "123 Main St", City = "Seattle", PostCode = "98101" },
            Tags = new[] { "electronics", "laptop" },
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        },
        new
        {
            PK = "CUSTOMER#alice",
            SK = "ORDER#002",
            Name = "Alice's Book",
            Status = "Delivered",
            Total = new { Amount = 29.99m, Currency = "USD" },
            Quantity = 3,
            ShippingAddress = new { Street = "123 Main St", City = "Seattle", PostCode = "98101" },
            Tags = new[] { "books" },
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        },
        new
        {
            PK = "CUSTOMER#alice",
            SK = "ORDER#003",
            Name = "Alice's Monitor",
            Status = "Processing",
            Total = new { Amount = 549.00m, Currency = "USD" },
            Quantity = 1,
            ShippingAddress = new { Street = "456 Oak Ave", City = "Portland", PostCode = "97201" },
            Tags = new[] { "electronics", "monitor" },
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        },
        new
        {
            PK = "CUSTOMER#bob",
            SK = "ORDER#004",
            Name = "Bob's Keyboard",
            Status = "Shipped",
            Total = new { Amount = 89.99m, Currency = "USD" },
            Quantity = 2,
            ShippingAddress = new { Street = "789 Elm St", City = "Austin", PostCode = "73301" },
            Tags = new[] { "electronics", "keyboard" },
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        },
        new
        {
            PK = "CUSTOMER#bob",
            SK = "ORDER#005",
            Name = "Bob's Notebook",
            Status = "Cancelled",
            Total = new { Amount = 12.50m, Currency = "USD" },
            Quantity = 5,
            ShippingAddress = new { Street = "789 Elm St", City = "Austin", PostCode = "73301" },
            Tags = new[] { "office", "notebook" },
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        }
    };

    foreach (var order in orders)
    {
        await client.PutItemAsync(new PutItemRequest
        {
            TableName = "Orders",
            Item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = order.PK },
                ["SK"] = new AttributeValue { S = order.SK },
                ["Name"] = new AttributeValue { S = order.Name },
                ["Status"] = new AttributeValue { S = order.Status },
                ["Total"] = new AttributeValue
                {
                    IsMSet = true,
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Amount"] = new AttributeValue { N = order.Total.Amount.ToString() },
                        ["Currency"] = new AttributeValue { S = order.Total.Currency }
                    }
                },
                ["Quantity"] = new AttributeValue { N = order.Quantity.ToString() },
                ["ShippingAddress"] = new AttributeValue
                {
                    IsMSet = true,
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Street"] = new AttributeValue { S = order.ShippingAddress.Street },
                        ["City"] = new AttributeValue { S = order.ShippingAddress.City },
                        ["PostCode"] = new AttributeValue { S = order.ShippingAddress.PostCode }
                    }
                },
                ["Tags"] = new AttributeValue { SS = order.Tags.ToList() },
                ["CreatedAt"] = new AttributeValue { S = order.CreatedAt.ToString("O") }
            }
        });
    }

    Console.WriteLine($"Seeded {orders.Length} orders.");
}
