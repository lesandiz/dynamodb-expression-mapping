# Spec 02: ASP.NET Web API Example

## Motivation

A production-like demo showing `DynamoDb.ExpressionMapping` in an ASP.NET Web API with dependency injection, repository pattern, REST endpoints, pagination, and Docker deployment. Demonstrates how the library integrates with real-world application architecture.

This example answers: *"How do I use this library in a production .NET service?"*

## Prerequisites

- .NET 8.0 SDK or later
- Docker and Docker Compose

## Project Structure

```
examples/WebApiExample/
├── Controllers/
│   ├── OrdersController.cs
│   ├── ProductsController.cs
│   └── CustomersController.cs
├── Models/
│   ├── Order.cs
│   ├── Product.cs
│   ├── Customer.cs
│   └── Address.cs
├── DTOs/
│   ├── OrderDto.cs
│   ├── OrderDetailDto.cs
│   ├── CreateOrderRequest.cs
│   ├── UpdateOrderRequest.cs
│   ├── ProductDto.cs
│   ├── CustomerDto.cs
│   └── PagedResponse.cs
├── Converters/
│   └── MoneyConverter.cs
├── Repositories/
│   ├── IOrderRepository.cs
│   └── OrderRepository.cs
├── Infrastructure/
│   ├── DynamoDbSeeder.cs
│   └── TableDefinitions.cs
├── Program.cs
├── WebApiExample.csproj
├── Dockerfile
├── docker-compose.yml
└── README.md
```

## Entity Models

### Single-Table Design

All entities share one table (`AppData`) with differentiated key patterns:

```
Table: AppData
  PK: string  (Hash key)
  SK: string  (Range key)

Key patterns:
  Customer: PK=CUSTOMER#<id>,  SK=PROFILE
  Order:    PK=CUSTOMER#<id>,  SK=ORDER#<id>
  Product:  PK=PRODUCT#<id>,   SK=METADATA
```

### `Order`

```csharp
using DynamoDb.ExpressionMapping.Attributes;

namespace WebApiExample.Models;

public class Order
{
    public string PK { get; set; } = default!;
    public string SK { get; set; } = default!;

    [DynamoDbAttribute("order_id")]
    public string OrderId { get; set; } = default!;

    [DynamoDbAttribute("customer_id")]
    public string CustomerId { get; set; } = default!;

    public string Name { get; set; } = default!;            // Reserved keyword
    public string Status { get; set; } = default!;          // Reserved keyword
    public Money Total { get; set; } = default!;
    public int Quantity { get; set; }
    public Address ShippingAddress { get; set; } = default!;
    public List<string> Tags { get; set; } = new();
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### `Product`

```csharp
namespace WebApiExample.Models;

public class Product
{
    public string PK { get; set; } = default!;
    public string SK { get; set; } = default!;

    [DynamoDbAttribute("product_id")]
    public string ProductId { get; set; } = default!;

    public string Name { get; set; } = default!;            // Reserved keyword
    public string Category { get; set; } = default!;
    public Money Price { get; set; } = default!;
    public int StockCount { get; set; }
    public bool IsActive { get; set; }
}
```

### `Customer`

```csharp
namespace WebApiExample.Models;

public class Customer
{
    public string PK { get; set; } = default!;
    public string SK { get; set; } = default!;

    [DynamoDbAttribute("customer_id")]
    public string CustomerId { get; set; } = default!;

    public string Name { get; set; } = default!;            // Reserved keyword
    public string Email { get; set; } = default!;
    public Address Address { get; set; } = default!;
    public DateTime JoinedAt { get; set; }
}
```

### `Address`

```csharp
namespace WebApiExample.Models;

public class Address
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string PostCode { get; set; } = default!;
}
```

### `Money`

```csharp
namespace WebApiExample.Models;

public record Money(decimal Amount, string Currency);
```

## Attribute Mapping

Combines annotation-based mapping (`[DynamoDbAttribute]`) with fluent overrides via `AddDynamoDbEntity<T>()`.

Annotations on models handle attribute name deviations (e.g., `order_id`, `customer_id`). Fluent overrides used in DI registration for any additional configuration that can't be expressed via attributes.

## Custom Converter

Same `MoneyConverter` as the console example, but registered via DI config builder:

```csharp
services.AddDynamoDbExpressionMapping(config =>
{
    config.WithConverter(new MoneyConverter());
});
```

## Infrastructure

### `docker-compose.yml`

```yaml
services:
  dynamodb-local:
    image: amazon/dynamodb-local:latest
    container_name: dynamodb-local
    ports:
      - "8000:8000"
    command: ["-jar", "DynamoDBLocal.jar", "-sharedDb", "-inMemory"]
    healthcheck:
      test: ["CMD-SHELL", "curl -sf http://localhost:8000/shell/ || exit 1"]
      interval: 5s
      timeout: 3s
      retries: 5

  api:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: webapi-example
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - DynamoDb__ServiceUrl=http://dynamodb-local:8000
    depends_on:
      dynamodb-local:
        condition: service_healthy
```

### `Dockerfile`

Multi-stage .NET build:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "WebApiExample.dll"]
```

### `DynamoDbSeeder` (IHostedService)

Runs on startup. Creates the `AppData` table (idempotent) and seeds sample data:

**Customers** (2):
| PK | SK | customer_id | Name | Email |
|---|---|---|---|---|
| CUSTOMER#alice | PROFILE | alice | Alice Johnson | alice@example.com |
| CUSTOMER#bob | PROFILE | bob | Bob Smith | bob@example.com |

**Orders** (5):
| PK | SK | order_id | customer_id | Name | Status | Total |
|---|---|---|---|---|---|---|
| CUSTOMER#alice | ORDER#001 | 001 | alice | Laptop | Shipped | 1299.99 USD |
| CUSTOMER#alice | ORDER#002 | 002 | alice | Book | Delivered | 29.99 USD |
| CUSTOMER#alice | ORDER#003 | 003 | alice | Monitor | Processing | 549.00 USD |
| CUSTOMER#bob | ORDER#004 | 004 | bob | Keyboard | Shipped | 89.99 USD |
| CUSTOMER#bob | ORDER#005 | 005 | bob | Notebook | Cancelled | 12.50 USD |

**Products** (3):
| PK | SK | product_id | Name | Category | Price | StockCount | IsActive |
|---|---|---|---|---|---|---|---|
| PRODUCT#laptop | METADATA | laptop | Pro Laptop 15" | Electronics | 1299.99 USD | 42 | true |
| PRODUCT#keyboard | METADATA | keyboard | Mechanical KB | Electronics | 89.99 USD | 150 | true |
| PRODUCT#notebook | METADATA | notebook | Spiral Notebook | Office | 12.50 USD | 0 | false |

## DI Setup

```csharp
// Program.cs

var builder = WebApplication.CreateBuilder(args);

// 1. DynamoDB client
builder.Services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var serviceUrl = builder.Configuration["DynamoDb:ServiceUrl"]
        ?? "http://localhost:8000";
    return new AmazonDynamoDBClient(new AmazonDynamoDBConfig
    {
        ServiceURL = serviceUrl
    });
});

// 2. Expression mapping with custom converter
builder.Services.AddDynamoDbExpressionMapping(config =>
{
    config.WithConverter(new MoneyConverter());
});

// 3. Per-entity configuration (fluent overrides supplement [DynamoDbAttribute])
builder.Services.AddDynamoDbEntity<Order>();
builder.Services.AddDynamoDbEntity<Product>();
builder.Services.AddDynamoDbEntity<Customer>();

// 4. Repositories
builder.Services.AddSingleton<IOrderRepository, OrderRepository>();

// 5. Seeder
builder.Services.AddHostedService<DynamoDbSeeder>();

// 6. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
```

## Repository Pattern

### `IOrderRepository`

```csharp
namespace WebApiExample.Repositories;

public interface IOrderRepository
{
    Task<PagedResponse<OrderDto>> QueryOrdersAsync(
        string customerId,
        string? statusFilter = null,
        int limit = 20,
        string? paginationToken = null);

    Task<OrderDetailDto?> GetOrderAsync(string customerId, string orderId);
    Task CreateOrderAsync(CreateOrderRequest request);
    Task UpdateOrderAsync(string customerId, string orderId, UpdateOrderRequest request);
    Task DeleteOrderAsync(string customerId, string orderId);
}
```

### `OrderRepository`

Injects all typed builders via constructor injection:

```csharp
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Extensions;
using DynamoDb.ExpressionMapping.ResultMapping;

namespace WebApiExample.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IAmazonDynamoDB _client;
    private readonly IProjectionBuilder<Order> _projectionBuilder;
    private readonly IFilterExpressionBuilder<Order> _filterBuilder;
    private readonly IConditionExpressionBuilder<Order> _conditionBuilder;
    private readonly IKeyConditionExpressionBuilder<Order> _keyConditionBuilder;
    private readonly IDirectResultMapper<Order> _resultMapper;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly IAttributeNameResolverFactory _resolverFactory;

    private const string TableName = "AppData";

    public OrderRepository(
        IAmazonDynamoDB client,
        IProjectionBuilder<Order> projectionBuilder,
        IFilterExpressionBuilder<Order> filterBuilder,
        IConditionExpressionBuilder<Order> conditionBuilder,
        IKeyConditionExpressionBuilder<Order> keyConditionBuilder,
        IDirectResultMapper<Order> resultMapper,
        IAttributeValueConverterRegistry converterRegistry,
        IAttributeNameResolverFactory resolverFactory)
    {
        _client = client;
        _projectionBuilder = projectionBuilder;
        _filterBuilder = filterBuilder;
        _conditionBuilder = conditionBuilder;
        _keyConditionBuilder = keyConditionBuilder;
        _resultMapper = resultMapper;
        _converterRegistry = converterRegistry;
        _resolverFactory = resolverFactory;
    }

    // ... implementation below
}
```

## REST Endpoints

### `GET /api/orders?customerId={id}&status={status}&limit={n}&token={token}`

Query orders for a customer with optional status filter and pagination.

```csharp
// In OrderRepository:
public async Task<PagedResponse<OrderDto>> QueryOrdersAsync(
    string customerId,
    string? statusFilter,
    int limit,
    string? paginationToken)
{
    // Compile mapper once (idempotent, cached internally)
    var mapper = _resultMapper.CreateMapper(o => new OrderDto
    {
        OrderId = o.OrderId,
        Name = o.Name,
        Status = o.Status,
        Quantity = o.Quantity
    });

    var request = new QueryRequest { TableName = TableName, Limit = limit }
        .WithKeyCondition(_keyConditionBuilder,
            b => b.WithPartitionKey(o => o.PK, $"CUSTOMER#{customerId}")
                  .WithSortKeyBeginsWith(o => o.SK, "ORDER#"))
        .WithProjection(_projectionBuilder,
            o => new { o.OrderId, o.Name, o.Status, o.Quantity });

    // Apply optional status filter dynamically
    if (!string.IsNullOrEmpty(statusFilter))
    {
        // Build filter independently and apply
        var filterResult = _filterBuilder.BuildFilter(o => o.Status == statusFilter);
        request.FilterExpression = filterResult.Expression;
        // Merge names/values (RequestMergeHelpers handles collision detection)
        foreach (var name in filterResult.ExpressionAttributeNames)
            request.ExpressionAttributeNames[name.Key] = name.Value;
        foreach (var val in filterResult.ExpressionAttributeValues)
            request.ExpressionAttributeValues[val.Key] = val.Value;
    }

    // Apply pagination token
    if (!string.IsNullOrEmpty(paginationToken))
    {
        request.ExclusiveStartKey = DecodePaginationToken(paginationToken);
    }

    var response = await _client.QueryAsync(request);

    return new PagedResponse<OrderDto>
    {
        Items = response.Items.Select(item => mapper(item)).ToList(),
        NextToken = response.LastEvaluatedKey?.Count > 0
            ? EncodePaginationToken(response.LastEvaluatedKey)
            : null
    };
}
```

**Controller**:
```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _repository;

    public OrdersController(IOrderRepository repository)
        => _repository = repository;

    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string customerId,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 20,
        [FromQuery] string? token = null)
    {
        var result = await _repository.QueryOrdersAsync(
            customerId, status, limit, token);
        return Ok(result);
    }
}
```

### `GET /api/orders/{customerId}/{orderId}`

Get a single order with full projection to `OrderDetailDto`.

```csharp
public async Task<OrderDetailDto?> GetOrderAsync(string customerId, string orderId)
{
    var mapper = _resultMapper.CreateMapper(o => new OrderDetailDto
    {
        OrderId = o.OrderId,
        CustomerId = o.CustomerId,
        Name = o.Name,
        Status = o.Status,
        Quantity = o.Quantity,
        City = o.ShippingAddress.City,
        Notes = o.Notes,
        CreatedAt = o.CreatedAt
    });

    var request = new GetItemRequest
    {
        TableName = TableName,
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"CUSTOMER#{customerId}" },
            ["SK"] = new AttributeValue { S = $"ORDER#{orderId}" }
        }
    }
    .WithProjection(_projectionBuilder,
        o => new { o.OrderId, o.CustomerId, o.Name, o.Status,
                   o.Quantity, o.ShippingAddress.City, o.Notes, o.CreatedAt });

    var response = await _client.GetItemAsync(request);

    if (!response.IsItemSet)
        return null;

    return mapper(response.Item);
}
```

### `POST /api/orders`

Create an order with a condition expression to prevent overwriting existing items.

```csharp
public async Task CreateOrderAsync(CreateOrderRequest request)
{
    var item = MapToItem(request); // Convert DTO to DynamoDB item dictionary

    var putRequest = new PutItemRequest
    {
        TableName = TableName,
        Item = item
    }
    .WithCondition(_conditionBuilder,
        o => o.PK == null); // attribute_not_exists(PK) — prevents overwrite

    try
    {
        await _client.PutItemAsync(putRequest);
    }
    catch (ConditionalCheckFailedException)
    {
        throw new InvalidOperationException(
            $"Order {request.OrderId} already exists for customer {request.CustomerId}.");
    }
}
```

**Note**: The predicate `o => o.PK == null` compiles to `attribute_not_exists(PK)` via the condition expression builder, which prevents accidental overwrites.

### `PUT /api/orders/{customerId}/{orderId}`

Partial update from request body using `UpdateExpressionBuilder`.

```csharp
public async Task UpdateOrderAsync(
    string customerId, string orderId, UpdateOrderRequest request)
{
    // Build update dynamically based on which fields are present
    var updateBuilder = new UpdateExpressionBuilder<Order>(
        _resolverFactory, _converterRegistry);

    if (request.Status is not null)
        updateBuilder.Set(o => o.Status, request.Status);

    if (request.Quantity.HasValue)
        updateBuilder.Set(o => o.Quantity, request.Quantity.Value);

    if (request.Notes is not null)
        updateBuilder.Set(o => o.Notes, request.Notes);

    var updateResult = updateBuilder.Build();

    if (updateResult.IsEmpty)
        return; // Nothing to update

    var updateRequest = new UpdateItemRequest
    {
        TableName = TableName,
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"CUSTOMER#{customerId}" },
            ["SK"] = new AttributeValue { S = $"ORDER#{orderId}" }
        }
    }
    .WithUpdate(updateResult);

    await _client.UpdateItemAsync(updateRequest);
}
```

### `DELETE /api/orders/{customerId}/{orderId}`

Delete with condition guard — only delete if the order exists.

```csharp
public async Task DeleteOrderAsync(string customerId, string orderId)
{
    var deleteRequest = new DeleteItemRequest
    {
        TableName = TableName,
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"CUSTOMER#{customerId}" },
            ["SK"] = new AttributeValue { S = $"ORDER#{orderId}" }
        }
    }
    .WithCondition(_conditionBuilder,
        o => o.PK != null); // attribute_exists(PK) — only delete if exists

    try
    {
        await _client.DeleteItemAsync(deleteRequest);
    }
    catch (ConditionalCheckFailedException)
    {
        throw new KeyNotFoundException(
            $"Order {orderId} not found for customer {customerId}.");
    }
}
```

### `GET /api/products?category={cat}&activeOnly={bool}`

Scan products with optional filters.

```csharp
// In ProductsController (uses injected builders directly, no repository layer)
[HttpGet]
public async Task<IActionResult> GetProducts(
    [FromQuery] string? category = null,
    [FromQuery] bool activeOnly = false)
{
    var mapper = _resultMapper.CreateMapper(p => new ProductDto
    {
        ProductId = p.ProductId,
        Name = p.Name,
        Category = p.Category,
        StockCount = p.StockCount,
        IsActive = p.IsActive
    });

    var request = new ScanRequest { TableName = "AppData" }
        .WithProjection(_projectionBuilder,
            p => new { p.ProductId, p.Name, p.Category, p.StockCount, p.IsActive });

    // Build and compose filters dynamically
    FilterExpressionResult? composedFilter = null;

    // Base filter: only product items (SK = "METADATA" and PK begins with "PRODUCT#")
    // This is handled by the key pattern check

    if (!string.IsNullOrEmpty(category))
    {
        var catFilter = _filterBuilder.BuildFilter(p => p.Category == category);
        composedFilter = catFilter;
    }

    if (activeOnly)
    {
        var activeFilter = _filterBuilder.BuildFilter(p => p.IsActive == true);
        composedFilter = composedFilter is not null
            ? FilterExpressionResult.And(composedFilter, activeFilter)
            : activeFilter;
    }

    if (composedFilter is not null)
    {
        request.FilterExpression = composedFilter.Expression;
        request.ExpressionAttributeNames = new Dictionary<string, string>(
            composedFilter.ExpressionAttributeNames);
        request.ExpressionAttributeValues = new Dictionary<string, AttributeValue>(
            composedFilter.ExpressionAttributeValues);
    }

    var response = await _client.ScanAsync(request);
    var products = response.Items.Select(item => mapper(item)).ToList();

    return Ok(products);
}
```

### `GET /api/customers/{id}`

Get a single customer with projection.

```csharp
// In CustomersController
[HttpGet("{id}")]
public async Task<IActionResult> GetCustomer(string id)
{
    var mapper = _resultMapper.CreateMapper(c => new CustomerDto
    {
        CustomerId = c.CustomerId,
        Name = c.Name,
        Email = c.Email,
        City = c.Address.City,
        JoinedAt = c.JoinedAt
    });

    var request = new GetItemRequest
    {
        TableName = "AppData",
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = $"CUSTOMER#{id}" },
            ["SK"] = new AttributeValue { S = "PROFILE" }
        }
    }
    .WithProjection(_projectionBuilder,
        c => new { c.CustomerId, c.Name, c.Email, c.Address.City, c.JoinedAt });

    var response = await _client.GetItemAsync(request);

    if (!response.IsItemSet)
        return NotFound();

    return Ok(mapper(response.Item));
}
```

## DTOs

### `OrderDto`

```csharp
public class OrderDto
{
    public string OrderId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int Quantity { get; set; }
}
```

### `OrderDetailDto`

```csharp
public class OrderDetailDto
{
    public string OrderId { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int Quantity { get; set; }
    public string City { get; set; } = default!;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### `CreateOrderRequest`

```csharp
public class CreateOrderRequest
{
    public string OrderId { get; set; } = default!;
    public string CustomerId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Status { get; set; } = "Processing";
    public int Quantity { get; set; } = 1;
    public decimal TotalAmount { get; set; }
    public string TotalCurrency { get; set; } = "USD";
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string PostCode { get; set; } = default!;
    public List<string> Tags { get; set; } = new();
}
```

### `UpdateOrderRequest`

```csharp
public class UpdateOrderRequest
{
    public string? Status { get; set; }
    public int? Quantity { get; set; }
    public string? Notes { get; set; }
}
```

### `ProductDto`

```csharp
public class ProductDto
{
    public string ProductId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public int StockCount { get; set; }
    public bool IsActive { get; set; }
}
```

### `CustomerDto`

```csharp
public class CustomerDto
{
    public string CustomerId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string City { get; set; } = default!;
    public DateTime JoinedAt { get; set; }
}
```

### `PagedResponse<T>`

```csharp
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextToken { get; set; }
}
```

## Pagination

Token-based pagination using Base64-encoded `LastEvaluatedKey`.

```csharp
private static string EncodePaginationToken(
    Dictionary<string, AttributeValue> lastEvaluatedKey)
{
    // Serialize to JSON, then Base64 encode
    var json = System.Text.Json.JsonSerializer.Serialize(
        lastEvaluatedKey.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.S ?? kvp.Value.N));
    return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
}

private static Dictionary<string, AttributeValue> DecodePaginationToken(string token)
{
    var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(token));
    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
    return dict.ToDictionary(
        kvp => kvp.Key,
        kvp => new AttributeValue { S = kvp.Value });
}
```

The `NextToken` is returned in the response body. Clients pass it back via `?token=` query parameter.

## Swagger/OpenAPI

Configured via `builder.Services.AddSwaggerGen()` and `app.UseSwagger()` / `app.UseSwaggerUI()`. Available at `http://localhost:5000/swagger`.

## Example Requests & Responses

### Query Orders

```bash
# All of Alice's orders
curl http://localhost:5000/api/orders?customerId=alice

# Filtered by status
curl http://localhost:5000/api/orders?customerId=alice&status=Shipped

# With pagination
curl "http://localhost:5000/api/orders?customerId=alice&limit=2"
```

**Response**:
```json
{
  "items": [
    { "orderId": "001", "name": "Laptop", "status": "Shipped", "quantity": 1 },
    { "orderId": "002", "name": "Book", "status": "Delivered", "quantity": 3 }
  ],
  "nextToken": "eyJQSyI6IkNVU1RPTUVSIzEyMyIsIlNLIjoiT1JERVIjMDAyIn0="
}
```

### Get Single Order

```bash
curl http://localhost:5000/api/orders/alice/001
```

**Response**:
```json
{
  "orderId": "001",
  "customerId": "alice",
  "name": "Laptop",
  "status": "Shipped",
  "quantity": 1,
  "city": "Seattle",
  "notes": null,
  "createdAt": "2025-01-15T10:30:00Z"
}
```

### Create Order

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "orderId": "006",
    "customerId": "alice",
    "name": "Headphones",
    "totalAmount": 199.99,
    "totalCurrency": "USD",
    "street": "123 Main St",
    "city": "Seattle",
    "postCode": "98101",
    "tags": ["electronics", "audio"]
  }'
```

**Response**: `201 Created`

**Duplicate attempt**: `409 Conflict` with error message.

### Update Order

```bash
curl -X PUT http://localhost:5000/api/orders/alice/001 \
  -H "Content-Type: application/json" \
  -d '{
    "status": "Delivered",
    "notes": "Left at front door"
  }'
```

**Response**: `204 No Content`

### Delete Order

```bash
curl -X DELETE http://localhost:5000/api/orders/bob/005
```

**Response**: `204 No Content`

**Non-existent order**: `404 Not Found`

### Get Products

```bash
# All products
curl http://localhost:5000/api/products

# Filter by category
curl "http://localhost:5000/api/products?category=Electronics"

# Active only
curl "http://localhost:5000/api/products?activeOnly=true"

# Combined
curl "http://localhost:5000/api/products?category=Electronics&activeOnly=true"
```

### Get Customer

```bash
curl http://localhost:5000/api/customers/alice
```

**Response**:
```json
{
  "customerId": "alice",
  "name": "Alice Johnson",
  "email": "alice@example.com",
  "city": "Seattle",
  "joinedAt": "2024-06-01T00:00:00Z"
}
```

## Build & Run

### Full Docker (recommended)

```bash
docker compose up --build
```

- API: `http://localhost:5000`
- Swagger: `http://localhost:5000/swagger`

### Local Development

```bash
# Start DynamoDB Local only
docker compose up dynamodb-local -d

# Run API locally
dotnet run
```

### Clean Up

```bash
docker compose down
```

## README.md Contents

The `examples/WebApiExample/README.md` should contain:

```markdown
# Web API Example

A production-like ASP.NET Web API showing DynamoDb.ExpressionMapping with
dependency injection, repository pattern, REST endpoints, and pagination.

## What's Demonstrated

- Full DI registration with `AddDynamoDbExpressionMapping()` and `AddDynamoDbEntity<T>()`
- Repository pattern with constructor-injected typed builders
- Single-table DynamoDB design (Customer, Order, Product share one table)
- Dynamic filter composition from query string parameters
- Partial updates from request body via `UpdateExpressionBuilder`
- Condition expressions for create guards and delete guards
- Token-based pagination with `LastEvaluatedKey` / `ExclusiveStartKey`
- Direct result mapping to typed DTOs
- Custom attribute mapping with `[DynamoDbAttribute]`
- Custom type converter (`MoneyConverter`) registered via DI
- Swagger/OpenAPI documentation
- Dockerized deployment (API + DynamoDB Local)

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- [Docker](https://www.docker.com/get-started) and Docker Compose

## Run (Docker)

    docker compose up --build

- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger

## Run (Local)

    docker compose up dynamodb-local -d
    dotnet run

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | /api/orders?customerId=&status=&limit=&token= | Query orders |
| GET | /api/orders/{customerId}/{orderId} | Get single order |
| POST | /api/orders | Create order |
| PUT | /api/orders/{customerId}/{orderId} | Update order |
| DELETE | /api/orders/{customerId}/{orderId} | Delete order |
| GET | /api/products?category=&activeOnly= | List products |
| GET | /api/customers/{id} | Get customer |

## Clean Up

    docker compose down
```

## Key Patterns Demonstrated

| Pattern | How |
|---|---|
| Full DI registration | `AddDynamoDbExpressionMapping(config => ...)` + `AddDynamoDbEntity<T>()` |
| Repository pattern | `IOrderRepository` with all typed builders injected |
| Dynamic filter composition | `FilterExpressionResult.And()` from query string params |
| Partial updates | `UpdateExpressionBuilder` conditionally adds SET operations |
| Create guard | `o => o.PK == null` → `attribute_not_exists(PK)` |
| Delete guard | `o => o.PK != null` → `attribute_exists(PK)` |
| Pagination | Base64-encoded `LastEvaluatedKey` as token |
| Single-table design | PK/SK patterns differentiate entity types |
| `[DynamoDbAttribute]` | `order_id`, `customer_id`, `product_id` |
| Custom converter (DI) | `MoneyConverter` registered via `config.WithConverter()` |
| Named DTO mapping | `CreateMapper(o => new OrderDto { ... })` |
| Nested property path | `o.ShippingAddress.City`, `c.Address.City` in projections |
| Swagger | `http://localhost:5000/swagger` for API exploration |

## Feature Coverage vs Console Example

| Feature | Console (Spec 01) | Web API (Spec 02) |
|---|---|---|
| Projection | Multi-property + reserved keywords | Per-endpoint DTOs |
| Filter | Basic + reserved keywords | Query-string driven dynamic |
| Filter Composition | `And`/`Or` chaining | Dynamic from params |
| Key Condition | PK + SK `begins_with` | Repository pattern |
| Update | SET/Increment/Remove/SetIfNotExists | Partial update from body |
| Condition | Conditional delete | Create guard + delete guard |
| Result Mapping | Anonymous + named DTO | Named DTOs |
| Custom Converter | `MoneyConverter` (manual) | `MoneyConverter` (DI) |
| Attribute Mapping | Convention only | `[DynamoDbAttribute]` + fluent |
| DI | Manual instantiation | Full `AddDynamoDbExpressionMapping()` |
| Pagination | N/A | Token-based |
| Multi-entity | Single (Order) | Order, Product, Customer |
| Docker | DynamoDB Local only | DynamoDB Local + API container |
