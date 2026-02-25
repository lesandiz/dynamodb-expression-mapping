# DynamoDb.ExpressionMapping

> **Disclaimer:** This repository and its code (library and examples) should be considered **experimental**. The implementation has been mostly generated from specs (see the [`.ralph/`](.ralph/) directory) and self-verified by an AI agent in a loop. **Not recommended for production use without thorough review and due scrutiny.**

A type-safe .NET library that converts C# LINQ expression trees into AWS DynamoDB expression strings (`ProjectionExpression`, `FilterExpression`, `ConditionExpression`, `UpdateExpression`, `KeyConditionExpression`) with direct result mapping that avoids full entity hydration.

[![NuGet](https://img.shields.io/nuget/v/DynamoDb.ExpressionMapping.svg)](https://www.nuget.org/packages/DynamoDb.ExpressionMapping/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Features

- **Type-Safe Expression Building** — Convert C# lambda expressions to DynamoDB expressions with compile-time checking
- **Direct Result Mapping** — Map `Dictionary<string, AttributeValue>` directly to projected types without full entity hydration
- **Automatic Keyword Aliasing** — 573+ DynamoDB reserved keywords automatically detected and aliased
- **Expression Caching** — Compiled expressions cached for performance
- **Fluent AWS SDK Integration** — Extension methods for all major request types
- **Pluggable Type Converters** — Extensible `AttributeValue` conversion system
- **Thread-Safe** — All builders are safe for concurrent use and singleton/DI registration
- **Minimal Dependencies** — Works alongside AWS SDK, not as a replacement

## Installation

```bash
dotnet add package DynamoDb.ExpressionMapping
```

**Requirements:** .NET 8.0+

## Quick Start

```csharp
using DynamoDb.ExpressionMapping;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.ResultMapping;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

// Define your entity
public class Order
{
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public decimal Total { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } // Reserved keyword - auto-aliased
    public DateTime CreatedAt { get; set; }
}

// Setup builders
var projectionBuilder = new ProjectionBuilder<Order>();
var filterBuilder = new FilterExpressionBuilder<Order>();
var resultMapper = new DirectResultMapper<Order>();

// 1. Build projection expression
var projection = projectionBuilder.BuildProjection(o => new
{
    o.OrderId,
    o.CustomerId,
    o.Total,
    o.Status
});

// 2. Build filter expression
var filter = filterBuilder.BuildFilter(o =>
    o.IsActive && o.Total > 100m);

// 3. Create scan request with expressions
var scanRequest = new ScanRequest { TableName = "Orders" }
    .WithProjection(projectionBuilder, o => new { o.OrderId, o.Total, o.Status })
    .WithFilter(filterBuilder, o => o.IsActive && o.Total > 100m);

// 4. Execute query
var client = new AmazonDynamoDBClient();
var response = await client.ScanAsync(scanRequest);

// 5. Map results directly to DTO
var mapper = resultMapper.CreateMapper(o => new { o.OrderId, o.Total, o.Status });
var orders = response.Items.Select(mapper).ToList();
```

## Expression Builders

### Projection Expressions

Build `ProjectionExpression` strings from C# selectors:

```csharp
var builder = new ProjectionBuilder<Order>();

// Single property
var result = builder.BuildProjection(o => o.OrderId);
// Result: "OrderId"

// Multiple properties (anonymous type)
var result = builder.BuildProjection(o => new { o.OrderId, o.CustomerId });
// Result: "OrderId, CustomerId"

// Nested properties
var result = builder.BuildProjection(o => o.Address.City);
// Result: "Address.City"

// Reserved keywords (auto-aliased)
var result = builder.BuildProjection(o => new { o.OrderId, o.Status });
// Result: "OrderId, #proj_0"
// ExpressionAttributeNames: { "#proj_0": "Status" }

// Method calls in selectors (transparent traversal)
// The builder extracts the underlying properties — methods run during result mapping
var result = builder.BuildProjection(o => new
{
    StatusEnum = Enum.Parse<OrderStatus>(o.Status),
    UpperName = o.Name.Trim().ToUpper(),
    o.Total
});
// Result: "#proj_0, #proj_1, Total"
// ExpressionAttributeNames: { "#proj_0": "Status", "#proj_1": "Name" }
```

> **Unsupported expressions:** Arithmetic (`p.Price * 1.1m`), string concatenation (`p.First + " " + p.Last`), conditionals (`p.IsActive ? p.StartDate : p.EndDate`), and array indexing (`p.Tags[0]`) will throw `UnsupportedExpressionException`. Use method calls or plain property access instead.

### Filter Expressions

Build `FilterExpression` and `ConditionExpression` strings from predicates:

```csharp
var filterBuilder = new FilterExpressionBuilder<Order>();

// Comparison operators
var result = filterBuilder.BuildFilter(o => o.Total > 100m);
// Result: "Total > :filt_v0"

// Boolean logic
var result = filterBuilder.BuildFilter(o =>
    o.IsActive && o.Total > 100m && o.Status == "Pending");

// String operations
var result = filterBuilder.BuildFilter(o => o.Title.StartsWith("Premium"));
// Result: "begins_with(Title, :filt_v0)"

var result = filterBuilder.BuildFilter(o => o.Description.Contains("sale"));
// Result: "contains(Description, :filt_v0)"

// Null checks
var result = filterBuilder.BuildFilter(o => o.EndDate == null);
// Result: "attribute_not_exists(EndDate)"

var result = filterBuilder.BuildFilter(o => o.EndDate != null);
// Result: "attribute_exists(EndDate)"

// IN operator
var statuses = new[] { "Pending", "Approved" };
var result = filterBuilder.BuildFilter(o => statuses.Contains(o.Status));
// Result: "#filt_0 IN (:filt_v0, :filt_v1)"

// Composable filters
var filter1 = filterBuilder.BuildFilter(o => o.IsActive);
var filter2 = filterBuilder.BuildFilter(o => o.Total > 100m);
var combined = filter1.And(filter2);
```

### Update Expressions

Build `UpdateExpression` strings with fluent API:

```csharp
var builder = new UpdateExpressionBuilder<Order>();

// Simple SET
var result = builder
    .Set(o => o.Status, "Shipped")
    .Build();
// Result: "SET Status = :upd_v0"

// Increment/Decrement
var result = builder
    .Increment(o => o.ViewCount, 1)
    .Decrement(o => o.Price, 10.5m)
    .Build();
// Result: "SET ViewCount = ViewCount + :upd_v0, Price = Price - :upd_v1"

// Conditional SET
var result = builder
    .SetIfNotExists(o => o.CreatedAt, DateTime.Now)
    .Build();
// Result: "SET CreatedAt = if_not_exists(CreatedAt, :upd_v0)"

// List operations
var result = builder
    .AppendToList(o => o.Tags, new List<string> { "new-tag" })
    .Build();
// Result: "SET Tags = list_append(Tags, :upd_v0)"

// Multiple clauses
var result = builder
    .Set(o => o.Status, "Updated")
    .Increment(o => o.ViewCount, 1)
    .Remove(o => o.TempFlag)
    .Build();
// Result: "SET Status = :upd_v0, ViewCount = ViewCount + :upd_v1 REMOVE TempFlag"
```

### Key Condition Expressions

Build `KeyConditionExpression` strings for Query operations:

```csharp
var builder = new KeyConditionExpressionBuilder<Order>();

// Partition key only
var result = builder
    .WithPartitionKey(e => e.PK, "USER#123")
    .Build();
// Result: "PK = :key_v0"

// Partition + Sort key equality
var result = builder
    .WithPartitionKey(e => e.PK, "USER#123")
    .WithSortKeyEquals(e => e.SK, "ORDER#456");
// Result: "PK = :key_v0 AND SK = :key_v1"

// Partition + Sort key comparison
var result = builder
    .WithPartitionKey(e => e.PK, "USER#123")
    .WithSortKeyGreaterThan(e => e.SK, "ORDER#100");
// Result: "PK = :key_v0 AND SK > :key_v1"

// Partition + Sort key BETWEEN
var result = builder
    .WithPartitionKey(e => e.PK, "USER#123")
    .WithSortKeyBetween(e => e.SK, "ORDER#100", "ORDER#999");
// Result: "PK = :key_v0 AND SK BETWEEN :key_v1 AND :key_v2"

// Partition + Sort key begins_with
var result = builder
    .WithPartitionKey(e => e.PK, "USER#123")
    .WithSortKeyBeginsWith(e => e.SK, "ORDER#2024-");
// Result: "PK = :key_v0 AND begins_with(SK, :key_v1)"
```

## Direct Result Mapping

Map DynamoDB results directly to projected types without full entity hydration:

```csharp
var mapper = new DirectResultMapper<Order>();

// Single property
var orderIdMapper = mapper.CreateMapper(o => o.OrderId);
var orderId = orderIdMapper(attributeDict);

// Anonymous type (DTO)
var dtoMapper = mapper.CreateMapper(o => new
{
    Id = o.OrderId,
    o.CustomerId,
    o.Total
});
var dto = dtoMapper(attributeDict);

// Named type
var summaryMapper = mapper.CreateMapper(o => new OrderSummary
{
    OrderId = o.OrderId,
    Total = o.Total,
    Status = o.Status
});
var summary = summaryMapper(attributeDict);

// Nested properties
var cityMapper = mapper.CreateMapper(o => o.Address.City);
var city = cityMapper(attributeDict);

// Use with query results
var response = await client.ScanAsync(scanRequest);
var dtoMapper = mapper.CreateMapper(o => new { o.OrderId, o.Total });
var results = response.Items.Select(dtoMapper).ToList();
```

## AWS SDK Integration

Extension methods for fluent request building:

```csharp
// Query with key condition, projection, and filter
var queryRequest = new QueryRequest { TableName = "Orders" }
    .WithKeyCondition(keyConditionBuilder, b => b
        .WithPartitionKey(e => e.PK, "USER#123")
        .WithSortKeyBeginsWith(e => e.SK, "ORDER#"))
    .WithProjection(projectionBuilder, o => new { o.OrderId, o.Total, o.Status })
    .WithFilter(filterBuilder, o => o.IsActive && o.Total > 100m);

// Scan with projection and filter
var scanRequest = new ScanRequest { TableName = "Orders" }
    .WithProjection(projectionBuilder, o => new { o.OrderId, o.Status })
    .WithFilter(filterBuilder, o => o.IsActive);

// UpdateItem with update expression and condition
var updateRequest = new UpdateItemRequest { TableName = "Orders" }
    .WithUpdate(updateBuilder, b => b
        .Set(e => e.Status, "Shipped")
        .Increment(e => e.ViewCount, 1))
    .WithCondition(conditionBuilder, o => o.Status == "Pending");

// PutItem with condition
var putRequest = new PutItemRequest { TableName = "Orders" }
    .WithCondition(conditionBuilder, o => o.OrderId == null);

// DeleteItem with condition
var deleteRequest = new DeleteItemRequest { TableName = "Orders" }
    .WithCondition(conditionBuilder, o => o.Status == "Draft");

// GetItem with projection
var getRequest = new GetItemRequest { TableName = "Orders" }
    .WithProjection(projectionBuilder, o => new { o.OrderId, o.Total });

// BatchGetItem with projection
var batchGetRequest = new BatchGetItemRequest()
    .WithProjection("Orders", projectionBuilder, o => new { o.OrderId, o.Status });
```

## Attribute Name Mapping

Customize how C# property names map to DynamoDB attribute names:

```csharp
using DynamoDb.ExpressionMapping.Attributes;

public class Product
{
    public Guid Id { get; set; }

    [DynamoDbAttribute("cust_id")]
    public Guid CustomerId { get; set; }

    [DynamoDbIgnore]
    public bool IsActive { get; set; }
}
```

**Resolution order:**
1. Fluent overrides (via `AttributeNameResolver`)
2. `[DynamoDbAttribute]` custom attribute
3. `[DynamoDBProperty]` (AWS SDK attribute)
4. Property name (convention)

## Type Converters

Built-in converters for common types:

- Primitives: `string`, `int`, `long`, `decimal`, `double`, `float`, `bool`
- Dates: `DateTime`, `DateTimeOffset`
- Binary: `byte[]`, `Guid`
- Collections: `List<T>`, `HashSet<T>`, `T[]`, `Dictionary<string, T>`
- Nullable types: `int?`, `DateTime?`, etc.
- Enums (string representation)

### Custom Converters

```csharp
using DynamoDb.ExpressionMapping.Mapping;

public class MoneyConverter : IAttributeValueConverter<Money>
{
    public AttributeValue ToAttributeValue(Money value)
    {
        return new AttributeValue { N = value.Amount.ToString("F2") };
    }

    public Money FromAttributeValue(AttributeValue value)
    {
        return new Money(decimal.Parse(value.N));
    }
}

// Apply to property
public class Order
{
    [DynamoDbConverter(typeof(MoneyConverter))]
    public Money Price { get; set; }
}

// Or register globally
var registry = AttributeValueConverterRegistry.Default.Clone();
registry.Register(new MoneyConverter());
```

## Dependency Injection

Register builders and configuration with Microsoft.Extensions.DependencyInjection:

```csharp
using DynamoDb.ExpressionMapping.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register all builders and mappers
services.AddDynamoDbExpressionMapping(config => config
    .WithNullHandling(NullHandlingMode.OmitNullValues)
    .WithCustomConverterRegistry(customRegistry));

// Register per-entity configuration
services.AddDynamoDbEntity<Order>(resolver => resolver
    .MapProperty(o => o.OrderId, "order_id")
    .MapProperty(o => o.CustomerId, "customer_id"));

// Inject into your services
public class OrderService
{
    private readonly ProjectionBuilder<Order> _projectionBuilder;
    private readonly FilterExpressionBuilder<Order> _filterBuilder;

    public OrderService(
        ProjectionBuilder<Order> projectionBuilder,
        FilterExpressionBuilder<Order> filterBuilder)
    {
        _projectionBuilder = projectionBuilder;
        _filterBuilder = filterBuilder;
    }
}
```

## Reserved Keywords

DynamoDB has 573+ reserved keywords. This library automatically detects and aliases them:

```csharp
public class Order
{
    public string OrderId { get; set; }
    public string Name { get; set; }      // Reserved keyword
    public string Status { get; set; }    // Reserved keyword
    public decimal Percent { get; set; }  // Reserved keyword
}

var result = builder.BuildProjection(o => new { o.OrderId, o.Name, o.Status });
// Result: "OrderId, #proj_0, #proj_1"
// ExpressionAttributeNames: { "#proj_0": "Name", "#proj_1": "Status" }
```

**Scoped alias prefixes prevent collisions:**
- Projection: `#proj_`, no value aliases
- Filter: `#filt_`, `:filt_v`
- Condition: `#cond_`, `:cond_v`
- Update: `#upd_`, `:upd_v`
- KeyCondition: `#key_`, `:key_v`

## Performance

- **Expression Caching** — Compiled expression delegates cached by default
- **Zero Allocations** — Hot path optimized to minimize allocations
- **Direct Mapping** — Avoid full entity hydration for partial projections
- **Compiled Delegates** — Result mappers run at native speed after initial compilation
- **Benchmarks** — BenchmarkDotNet baselines available in `tests/DynamoDb.ExpressionMapping.Benchmarks/`

### Comparing Benchmarks Against Baselines

Committed baseline results live in `tests/DynamoDb.ExpressionMapping.Benchmarks/baselines/`. To check for performance regressions:

```bash
# 1. Run benchmarks (generates results in BenchmarkDotNet.Artifacts/results/)
dotnet run -c Release --project tests/DynamoDb.ExpressionMapping.Benchmarks

# Run a specific benchmark class
dotnet run -c Release --project tests/DynamoDb.ExpressionMapping.Benchmarks -- --filter "*ProjectionBuilder*"
```

Compare against baselines locally or via the **Benchmarks** GitHub Actions workflow (manually triggered from the Actions tab), which runs all benchmarks, compares against baselines, and surfaces a regression summary in the step summary:

```bash
# Local comparison (requires jq)
bash .github/scripts/compare-benchmarks.sh \
  tests/DynamoDb.ExpressionMapping.Benchmarks/baselines \
  BenchmarkDotNet.Artifacts/results
```

Regression thresholds (from [PR-04](.ralph/prod-readiness/specs/PR-04-benchmarking.md)):

| Metric              | Threshold                 |
| ------------------- | ------------------------- |
| Mean execution time | > 20% regression          |
| Memory allocation   | > 50% regression          |
| Gen0 GC collections | Any increase on hot paths |

## Testing

```bash
# Unit & property-based tests (no Docker required)
dotnet test tests/DynamoDb.ExpressionMapping.Tests/

# Integration tests (uses Testcontainers — requires Docker)
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/
```

Soak & concurrency tests run against DynamoDB Local:

```bash
docker compose -f tests/DynamoDb.ExpressionMapping.SoakTests/docker-compose.yml up -d
dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests/ -- --concurrency-scenarios
dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests/ -- --duration 2
docker compose -f tests/DynamoDb.ExpressionMapping.SoakTests/docker-compose.yml down
```

## Building

```bash
dotnet build
dotnet pack
```

## Example: Full Pipeline

```csharp
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.ResultMapping;

// Setup
var client = new AmazonDynamoDBClient();
var projectionBuilder = new ProjectionBuilder<Order>();
var filterBuilder = new FilterExpressionBuilder<Order>();
var resultMapper = new DirectResultMapper<Order>();

// 1. Build scan request
var scanRequest = new ScanRequest { TableName = "Orders" }
    .WithProjection(projectionBuilder, o => new
    {
        o.OrderId,
        o.CustomerId,
        o.Total,
        o.Status
    })
    .WithFilter(filterBuilder, o =>
        o.IsActive && o.Total > 100m);

// 2. Execute query
var response = await client.ScanAsync(scanRequest);

// 3. Map results directly to DTO
var mapper = resultMapper.CreateMapper(o => new
{
    o.OrderId,
    o.CustomerId,
    o.Total,
    o.Status
});

var orders = response.Items
    .Select(mapper)
    .ToList();

// 4. Use results
foreach (var order in orders)
{
    Console.WriteLine($"Order {order.OrderId}: ${order.Total} - {order.Status}");
}
```

## Contributing

Contributions welcome! Please open an issue or PR.

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Dependencies

- `AWSSDK.DynamoDBv2` (>= 3.7.x)
- `Microsoft.Extensions.Logging.Abstractions` (>= 8.0.0) — optional
- `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 8.0.0) — optional
- `Microsoft.Extensions.Options` (>= 8.0.0) — optional

## Links

- [NuGet Package](https://www.nuget.org/packages/DynamoDb.ExpressionMapping/)
- [GitHub Repository](https://github.com/lesandiz/dynamodb-expression-mapping)
- [Issue Tracker](https://github.com/lesandiz/dynamodb-expression-mapping/issues)
