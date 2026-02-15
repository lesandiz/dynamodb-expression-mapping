using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ConsoleQuickStart;
using DynamoDb.ExpressionMapping;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
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

// === Scenario 1: Projection with reserved keywords ===
Console.WriteLine("=== Scenario 1: Projection ===");

var getRequest = new GetItemRequest
{
    TableName = "Orders",
    Key = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = "CUSTOMER#alice" },
        ["SK"] = new AttributeValue { S = "ORDER#001" }
    }
}.WithProjection(projectionBuilder, o => new { o.Name, o.Status, o.Quantity });

var getResponse = await client.GetItemAsync(getRequest);

Console.WriteLine($"  Name: {getResponse.Item["Name"].S}");
Console.WriteLine($"  Status: {getResponse.Item["Status"].S}");
Console.WriteLine($"  Quantity: {getResponse.Item["Quantity"].N}");
Console.WriteLine();

// === Scenario 2: Filter Expression ===
Console.WriteLine("=== Scenario 2: Filter ===");

var scanRequest = new ScanRequest { TableName = "Orders" }
    .WithFilter(filterBuilder,
        o => o.Status == "Shipped" && o.Quantity >= 1);

var scanResponse = await client.ScanAsync(scanRequest);

Console.WriteLine($"  Found {scanResponse.Items.Count} shipped orders with quantity >= 1:");
foreach (var item in scanResponse.Items)
{
    Console.WriteLine($"    - {item["Name"].S} (Qty: {item["Quantity"].N})");
}
Console.WriteLine();

// === Scenario 3: Filter Composition ===
Console.WriteLine("=== Scenario 3: Filter Composition ===");

var shippedFilter = filterBuilder.BuildFilter(o => o.Status == "Shipped");
var deliveredFilter = filterBuilder.BuildFilter(o => o.Status == "Delivered");

// Compose: (Status == "Shipped") OR (Status == "Delivered")
var composedFilter = FilterExpressionResult.Or(shippedFilter, deliveredFilter);

var composedScan = new ScanRequest
{
    TableName = "Orders",
    FilterExpression = composedFilter.Expression,
    ExpressionAttributeNames = new Dictionary<string, string>(composedFilter.ExpressionAttributeNames),
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>(composedFilter.ExpressionAttributeValues)
};

var composedResponse = await client.ScanAsync(composedScan);

Console.WriteLine($"  Found {composedResponse.Items.Count} shipped or delivered orders:");
foreach (var item in composedResponse.Items)
{
    Console.WriteLine($"    - {item["Name"].S} ({item["Status"].S})");
}
Console.WriteLine();

// === Scenario 4: Key Condition Query ===
Console.WriteLine("=== Scenario 4: Key Condition Query ===");

var queryRequest = new QueryRequest { TableName = "Orders" }
    .WithKeyCondition(keyConditionBuilder,
        b => b.WithPartitionKey(o => o.PK, "CUSTOMER#alice")
              .WithSortKeyBeginsWith(o => o.SK, "ORDER#"));

var queryResponse = await client.QueryAsync(queryRequest);

Console.WriteLine($"  Alice's orders ({queryResponse.Items.Count}):");
foreach (var item in queryResponse.Items)
{
    Console.WriteLine($"    - {item["SK"].S}: {item["Name"].S}");
}
Console.WriteLine();

// === Scenario 5: Update Expression ===
Console.WriteLine("=== Scenario 5: Update Expression ===");

var updateResult = new UpdateExpressionBuilder<Order>(resolverFactory, config.ConverterRegistry)
    .Set(o => o.Status, "Delivered")
    .Increment(o => o.Quantity, 1)
    .SetIfNotExists(o => o.Notes, "No notes provided")
    .Remove(o => o.Tags)
    .Build();

var updateRequest = new UpdateItemRequest
{
    TableName = "Orders",
    Key = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = "CUSTOMER#alice" },
        ["SK"] = new AttributeValue { S = "ORDER#001" }
    },
    ReturnValues = ReturnValue.ALL_NEW
}
.WithUpdate(updateResult);

var updateResponse = await client.UpdateItemAsync(updateRequest);

Console.WriteLine($"  Updated order:");
Console.WriteLine($"    Status: {updateResponse.Attributes["Status"].S}");
Console.WriteLine($"    Quantity: {updateResponse.Attributes["Quantity"].N}");
Console.WriteLine($"    Notes: {updateResponse.Attributes["Notes"].S}");
Console.WriteLine($"    Tags removed: {!updateResponse.Attributes.ContainsKey("Tags")}");
Console.WriteLine();

// === Scenario 6: Conditional Delete ===
Console.WriteLine("=== Scenario 6: Conditional Delete ===");

// First delete: bob/005 (Status = "Cancelled") — should succeed
try
{
    var deleteRequest1 = new DeleteItemRequest
    {
        TableName = "Orders",
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "CUSTOMER#bob" },
            ["SK"] = new AttributeValue { S = "ORDER#005" }
        }
    }.WithCondition(conditionBuilder, o => o.Status == "Cancelled");

    await client.DeleteItemAsync(deleteRequest1);
    Console.WriteLine("  Deleted Bob's cancelled notebook order.");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("  Delete skipped — Bob's notebook is not cancelled.");
}

// Second delete: bob/004 (Status = "Shipped") — should fail
try
{
    var deleteRequest2 = new DeleteItemRequest
    {
        TableName = "Orders",
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "CUSTOMER#bob" },
            ["SK"] = new AttributeValue { S = "ORDER#004" }
        }
    }.WithCondition(conditionBuilder, o => o.Status == "Cancelled");

    await client.DeleteItemAsync(deleteRequest2);
    Console.WriteLine("  Deleted Bob's cancelled keyboard order.");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("  Delete skipped — Bob's keyboard is not cancelled.");
}

Console.WriteLine();

// === Scenario 7: Result Mapping (Anonymous Type) ===
Console.WriteLine("=== Scenario 7: Result Mapping (Anonymous) ===");

var anonymousMapper = resultMapper.CreateMapper(o => new { o.Name, o.Status, o.Quantity });

var getRequest7 = new GetItemRequest
{
    TableName = "Orders",
    Key = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = "CUSTOMER#alice" },
        ["SK"] = new AttributeValue { S = "ORDER#002" }
    }
};

var getResponse7 = await client.GetItemAsync(getRequest7);
var mapped7 = anonymousMapper(getResponse7.Item);

Console.WriteLine($"  Name: {mapped7.Name}");
Console.WriteLine($"  Status: {mapped7.Status}");
Console.WriteLine($"  Quantity: {mapped7.Quantity}");
Console.WriteLine();

// === Scenario 8: Result Mapping (Named DTO) ===
Console.WriteLine("=== Scenario 8: Result Mapping (Named DTO) ===");

var dtoMapper = resultMapper.CreateMapper(o => new OrderSummary
{
    OrderId = o.SK,
    CustomerName = o.Name,
    Status = o.Status,
    City = o.ShippingAddress.City
});

var queryRequest8 = new QueryRequest
{
    TableName = "Orders",
    KeyConditionExpression = "PK = :pk",
    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
    {
        [":pk"] = new AttributeValue { S = "CUSTOMER#alice" }
    }
};

var queryResponse8 = await client.QueryAsync(queryRequest8);

Console.WriteLine($"  Alice's orders as OrderSummary DTOs:");
foreach (var item in queryResponse8.Items)
{
    var dto = dtoMapper(item);
    Console.WriteLine($"    - {dto.OrderId}: {dto.CustomerName} [{dto.Status}] ({dto.City})");
}
Console.WriteLine();

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
