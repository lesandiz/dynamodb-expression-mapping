using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using WebApiExample.Models;

namespace WebApiExample.Infrastructure;

public class DynamoDbSeeder : IHostedService
{
    private readonly IAmazonDynamoDB _client;
    private readonly ILogger<DynamoDbSeeder> _logger;
    private const string TableName = "AppData";

    public DynamoDbSeeder(IAmazonDynamoDB client, ILogger<DynamoDbSeeder> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await CreateTableAsync(cancellationToken);
        await SeedDataAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateTableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new CreateTableRequest
            {
                TableName = TableName,
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
            };

            await _client.CreateTableAsync(request, cancellationToken);
            _logger.LogInformation("Table '{TableName}' created successfully.", TableName);

            // Wait for table to become active
            await WaitForTableActiveAsync(cancellationToken);
        }
        catch (ResourceInUseException)
        {
            _logger.LogInformation("Table '{TableName}' already exists.", TableName);
        }
    }

    private async Task WaitForTableActiveAsync(CancellationToken cancellationToken)
    {
        var maxAttempts = 20;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            var response = await _client.DescribeTableAsync(TableName, cancellationToken);

            if (response.Table.TableStatus == TableStatus.ACTIVE)
            {
                _logger.LogInformation("Table '{TableName}' is now active.", TableName);
                return;
            }

            attempt++;
            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"Table '{TableName}' did not become active within expected time.");
    }

    private async Task SeedDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Seeding data into '{TableName}'...", TableName);

        // Check if data already exists
        var scanResponse = await _client.ScanAsync(new ScanRequest
        {
            TableName = TableName,
            Limit = 1
        }, cancellationToken);

        if (scanResponse.Items.Count > 0)
        {
            _logger.LogInformation("Table '{TableName}' already contains data. Skipping seed.", TableName);
            return;
        }

        // Seed Customers
        await SeedCustomersAsync(cancellationToken);

        // Seed Orders
        await SeedOrdersAsync(cancellationToken);

        // Seed Products
        await SeedProductsAsync(cancellationToken);

        _logger.LogInformation("Data seeding completed successfully.");
    }

    private async Task SeedCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = new[]
        {
            new
            {
                PK = "CUSTOMER#alice",
                SK = "PROFILE",
                customer_id = "alice",
                Name = "Alice Johnson",
                Email = "alice@example.com",
                Address = new { Street = "123 Main St", City = "Seattle", PostCode = "98101" },
                JoinedAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                PK = "CUSTOMER#bob",
                SK = "PROFILE",
                customer_id = "bob",
                Name = "Bob Smith",
                Email = "bob@example.com",
                Address = new { Street = "456 Oak Ave", City = "Portland", PostCode = "97201" },
                JoinedAt = new DateTime(2024, 7, 15, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        foreach (var customer in customers)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = customer.PK },
                ["SK"] = new AttributeValue { S = customer.SK },
                ["customer_id"] = new AttributeValue { S = customer.customer_id },
                ["Name"] = new AttributeValue { S = customer.Name },
                ["Email"] = new AttributeValue { S = customer.Email },
                ["Address"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Street"] = new AttributeValue { S = customer.Address.Street },
                        ["City"] = new AttributeValue { S = customer.Address.City },
                        ["PostCode"] = new AttributeValue { S = customer.Address.PostCode }
                    }
                },
                ["JoinedAt"] = new AttributeValue { S = customer.JoinedAt.ToString("o") }
            };

            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = TableName,
                Item = item
            }, cancellationToken);
        }

        _logger.LogInformation("Seeded {Count} customers.", customers.Length);
    }

    private async Task SeedOrdersAsync(CancellationToken cancellationToken)
    {
        var orders = new[]
        {
            new
            {
                PK = "CUSTOMER#alice",
                SK = "ORDER#001",
                order_id = "001",
                customer_id = "alice",
                Name = "Laptop",
                Status = "Shipped",
                Total = new { Amount = 1299.99m, Currency = "USD" },
                Quantity = 1,
                ShippingAddress = new { Street = "123 Main St", City = "Seattle", PostCode = "98101" },
                Tags = new[] { "electronics", "computer" },
                CreatedAt = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            },
            new
            {
                PK = "CUSTOMER#alice",
                SK = "ORDER#002",
                order_id = "002",
                customer_id = "alice",
                Name = "Book",
                Status = "Delivered",
                Total = new { Amount = 29.99m, Currency = "USD" },
                Quantity = 3,
                ShippingAddress = new { Street = "123 Main St", City = "Seattle", PostCode = "98101" },
                Tags = new[] { "books", "education" },
                CreatedAt = new DateTime(2025, 1, 16, 14, 20, 0, DateTimeKind.Utc)
            },
            new
            {
                PK = "CUSTOMER#alice",
                SK = "ORDER#003",
                order_id = "003",
                customer_id = "alice",
                Name = "Monitor",
                Status = "Processing",
                Total = new { Amount = 549.00m, Currency = "USD" },
                Quantity = 1,
                ShippingAddress = new { Street = "123 Main St", City = "Seattle", PostCode = "98101" },
                Tags = new[] { "electronics", "display" },
                CreatedAt = new DateTime(2025, 1, 17, 9, 45, 0, DateTimeKind.Utc)
            },
            new
            {
                PK = "CUSTOMER#bob",
                SK = "ORDER#004",
                order_id = "004",
                customer_id = "bob",
                Name = "Keyboard",
                Status = "Shipped",
                Total = new { Amount = 89.99m, Currency = "USD" },
                Quantity = 2,
                ShippingAddress = new { Street = "456 Oak Ave", City = "Portland", PostCode = "97201" },
                Tags = new[] { "electronics", "peripherals" },
                CreatedAt = new DateTime(2025, 1, 18, 11, 0, 0, DateTimeKind.Utc)
            },
            new
            {
                PK = "CUSTOMER#bob",
                SK = "ORDER#005",
                order_id = "005",
                customer_id = "bob",
                Name = "Notebook",
                Status = "Cancelled",
                Total = new { Amount = 12.50m, Currency = "USD" },
                Quantity = 5,
                ShippingAddress = new { Street = "456 Oak Ave", City = "Portland", PostCode = "97201" },
                Tags = new[] { "office", "stationery" },
                CreatedAt = new DateTime(2025, 1, 19, 16, 30, 0, DateTimeKind.Utc)
            }
        };

        foreach (var order in orders)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = order.PK },
                ["SK"] = new AttributeValue { S = order.SK },
                ["order_id"] = new AttributeValue { S = order.order_id },
                ["customer_id"] = new AttributeValue { S = order.customer_id },
                ["Name"] = new AttributeValue { S = order.Name },
                ["Status"] = new AttributeValue { S = order.Status },
                ["Total"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Amount"] = new AttributeValue { N = order.Total.Amount.ToString() },
                        ["Currency"] = new AttributeValue { S = order.Total.Currency }
                    }
                },
                ["Quantity"] = new AttributeValue { N = order.Quantity.ToString() },
                ["ShippingAddress"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Street"] = new AttributeValue { S = order.ShippingAddress.Street },
                        ["City"] = new AttributeValue { S = order.ShippingAddress.City },
                        ["PostCode"] = new AttributeValue { S = order.ShippingAddress.PostCode }
                    }
                },
                ["Tags"] = new AttributeValue
                {
                    L = order.Tags.Select(tag => new AttributeValue { S = tag }).ToList()
                },
                ["CreatedAt"] = new AttributeValue { S = order.CreatedAt.ToString("o") }
            };

            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = TableName,
                Item = item
            }, cancellationToken);
        }

        _logger.LogInformation("Seeded {Count} orders.", orders.Length);
    }

    private async Task SeedProductsAsync(CancellationToken cancellationToken)
    {
        var products = new[]
        {
            new
            {
                PK = "PRODUCT#laptop",
                SK = "METADATA",
                product_id = "laptop",
                Name = "Pro Laptop 15\"",
                Category = "Electronics",
                Price = new { Amount = 1299.99m, Currency = "USD" },
                StockCount = 42,
                IsActive = true
            },
            new
            {
                PK = "PRODUCT#keyboard",
                SK = "METADATA",
                product_id = "keyboard",
                Name = "Mechanical KB",
                Category = "Electronics",
                Price = new { Amount = 89.99m, Currency = "USD" },
                StockCount = 150,
                IsActive = true
            },
            new
            {
                PK = "PRODUCT#notebook",
                SK = "METADATA",
                product_id = "notebook",
                Name = "Spiral Notebook",
                Category = "Office",
                Price = new { Amount = 12.50m, Currency = "USD" },
                StockCount = 0,
                IsActive = false
            }
        };

        foreach (var product in products)
        {
            var item = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = product.PK },
                ["SK"] = new AttributeValue { S = product.SK },
                ["product_id"] = new AttributeValue { S = product.product_id },
                ["Name"] = new AttributeValue { S = product.Name },
                ["Category"] = new AttributeValue { S = product.Category },
                ["Price"] = new AttributeValue
                {
                    M = new Dictionary<string, AttributeValue>
                    {
                        ["Amount"] = new AttributeValue { N = product.Price.Amount.ToString() },
                        ["Currency"] = new AttributeValue { S = product.Price.Currency }
                    }
                },
                ["StockCount"] = new AttributeValue { N = product.StockCount.ToString() },
                ["IsActive"] = new AttributeValue { BOOL = product.IsActive }
            };

            await _client.PutItemAsync(new PutItemRequest
            {
                TableName = TableName,
                Item = item
            }, cancellationToken);
        }

        _logger.LogInformation("Seeded {Count} products.", products.Length);
    }
}
