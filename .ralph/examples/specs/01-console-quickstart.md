# Spec 01: Console Quick Start Example

## Motivation

The simplest possible runnable demo of `DynamoDb.ExpressionMapping`. A single `Program.cs` file with no dependency injection, no web framework, no repository pattern. Demonstrates all five expression types, custom type converters, filter composition, and direct result mapping against DynamoDB Local in Docker.

This example answers: *"How do I use this library in the simplest way possible?"*

## Prerequisites

- .NET 8.0 SDK or later
- Docker (for DynamoDB Local)

## Project Structure

```
examples/ConsoleQuickStart/
├── Program.cs                  # All demo code
├── Models.cs                   # Entity, nested type, DTO, custom converter
├── ConsoleQuickStart.csproj
├── docker-compose.yml          # DynamoDB Local
└── README.md                   # Getting started guide
```

## Entity Models

### `Order` Entity

```csharp
namespace ConsoleQuickStart;

public class Order
{
    public string PK { get; set; } = default!;          // CUSTOMER#<id>
    public string SK { get; set; } = default!;          // ORDER#<id>
    public string Name { get; set; } = default!;        // Reserved keyword in DynamoDB
    public string Status { get; set; } = default!;      // Reserved keyword in DynamoDB
    public Address ShippingAddress { get; set; } = default!;
    public Money Total { get; set; } = default!;
    public List<string> Tags { get; set; } = new();
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### `Address` (Nested Type)

```csharp
public class Address
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string PostCode { get; set; } = default!;
}
```

### `Money` (Custom Type Requiring Converter)

```csharp
public record Money(decimal Amount, string Currency);
```

### `OrderSummary` (Named DTO for Result Mapping)

```csharp
public class OrderSummary
{
    public string OrderId { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string City { get; set; } = default!;
}
```

## Custom Converter

`MoneyConverter` implements `IAttributeValueConverter<Money>`. Stores as a DynamoDB Map (`M`) with `Amount` (N) and `Currency` (S) keys.

```csharp
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;

public class MoneyConverter : IAttributeValueConverter<Money>
{
    public Type TargetType => typeof(Money);

    public AttributeValue ToAttributeValue(Money value) => new()
    {
        IsMSet = true,
        M = new Dictionary<string, AttributeValue>
        {
            ["Amount"] = new AttributeValue { N = value.Amount.ToString() },
            ["Currency"] = new AttributeValue { S = value.Currency }
        }
    };

    public Money FromAttributeValue(AttributeValue attributeValue)
    {
        var map = attributeValue.M;
        return new Money(
            decimal.Parse(map["Amount"].N),
            map["Currency"].S);
    }

    // Explicit interface implementation for non-generic interface
    AttributeValue IAttributeValueConverter.ToAttributeValue(object value)
        => ToAttributeValue((Money)value);

    object IAttributeValueConverter.FromAttributeValue(AttributeValue attributeValue)
        => FromAttributeValue(attributeValue);
}
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
```

### Table Schema

```
Table: Orders
  PK: string  (Hash key)   — pattern: CUSTOMER#<id>
  SK: string  (Range key)  — pattern: ORDER#<id>
```

Table creation via AWS SDK in `Program.cs` (idempotent — catches `ResourceInUseException`).

## Manual Instantiation (No DI)

All builders are instantiated manually using `DynamoDbExpressionConfig.Builder` and `AttributeNameResolverFactory`:

```csharp
using Amazon.DynamoDBv2;
using DynamoDb.ExpressionMapping;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ResultMapping;

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

// 4. Create DynamoDB client pointing at local
var client = new AmazonDynamoDBClient(new AmazonDynamoDBConfig
{
    ServiceURL = "http://localhost:8000"
});
```

## Seed Data

5 orders across 2 customers, inserted via `PutItemRequest`:

| PK | SK | Name | Status | Total | Quantity | City | Tags |
|---|---|---|---|---|---|---|---|
| CUSTOMER#alice | ORDER#001 | Alice's Laptop | Shipped | 1299.99 USD | 1 | Seattle | [electronics, laptop] |
| CUSTOMER#alice | ORDER#002 | Alice's Book | Delivered | 29.99 USD | 3 | Seattle | [books] |
| CUSTOMER#alice | ORDER#003 | Alice's Monitor | Processing | 549.00 USD | 1 | Portland | [electronics, monitor] |
| CUSTOMER#bob | ORDER#004 | Bob's Keyboard | Shipped | 89.99 USD | 2 | Austin | [electronics, keyboard] |
| CUSTOMER#bob | ORDER#005 | Bob's Notebook | Cancelled | 12.50 USD | 5 | Austin | [office, notebook] |

## Scenarios

### Scenario 1: Projection with Reserved Keywords

Demonstrates `ProjectionBuilder` extracting multiple properties including `Name` and `Status` (both DynamoDB reserved keywords that require automatic aliasing).

```csharp
using DynamoDb.ExpressionMapping.Extensions;

Console.WriteLine("=== Scenario 1: Projection ===");

var getRequest = new GetItemRequest
{
    TableName = "Orders",
    Key = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = "CUSTOMER#alice" },
        ["SK"] = new AttributeValue { S = "ORDER#001" }
    }
}
.WithProjection(projectionBuilder,
    o => new { o.Name, o.Status, o.Quantity });

var getResponse = await client.GetItemAsync(getRequest);

Console.WriteLine($"  Name: {getResponse.Item["Name"].S}");
Console.WriteLine($"  Status: {getResponse.Item["Status"].S}");
Console.WriteLine($"  Quantity: {getResponse.Item["Quantity"].N}");
```

**Key point**: `Name` and `Status` are auto-aliased to `#proj_Name` / `#proj_Status` in the `ProjectionExpression`. The consumer never sees the aliasing.

### Scenario 2: Filter Expression

Demonstrates `FilterExpressionBuilder` with comparison operators on properties including reserved keywords.

```csharp
Console.WriteLine("\n=== Scenario 2: Filter ===");

var scanRequest = new ScanRequest { TableName = "Orders" }
    .WithFilter(filterBuilder,
        o => o.Status == "Shipped" && o.Quantity >= 1);

var scanResponse = await client.ScanAsync(scanRequest);

Console.WriteLine($"  Found {scanResponse.Items.Count} shipped orders with quantity >= 1:");
foreach (var item in scanResponse.Items)
{
    Console.WriteLine($"    - {item["Name"].S} (Qty: {item["Quantity"].N})");
}
```

### Scenario 3: Filter Composition

Demonstrates composing independently-built `FilterExpressionResult` objects with `And()` and `Or()`. Each filter has independently scoped aliases — composition re-aliases to prevent collisions.

```csharp
Console.WriteLine("\n=== Scenario 3: Filter Composition ===");

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
```

### Scenario 4: Key Condition Query

Demonstrates `KeyConditionExpressionBuilder` with partition key equality and sort key `begins_with` via the staged fluent API.

```csharp
Console.WriteLine("\n=== Scenario 4: Key Condition Query ===");

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
```

### Scenario 5: Update Expression

Demonstrates `UpdateExpressionBuilder` fluent API with SET, Increment, SetIfNotExists, and Remove operations.

```csharp
Console.WriteLine("\n=== Scenario 5: Update ===");

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
```

**Note**: `UpdateExpressionBuilder` is not reusable — a new instance is created per update operation because it accumulates state.

### Scenario 6: Conditional Delete

Demonstrates `ConditionExpressionBuilder` with a condition expression on `DeleteItemRequest`, and handling `ConditionalCheckFailedException`.

```csharp
Console.WriteLine("\n=== Scenario 6: Conditional Delete ===");

// Delete Bob's cancelled notebook — only if status is "Cancelled"
try
{
    var deleteRequest = new DeleteItemRequest
    {
        TableName = "Orders",
        Key = new Dictionary<string, AttributeValue>
        {
            ["PK"] = new AttributeValue { S = "CUSTOMER#bob" },
            ["SK"] = new AttributeValue { S = "ORDER#005" }
        }
    }
    .WithCondition(conditionBuilder,
        o => o.Status == "Cancelled");

    await client.DeleteItemAsync(deleteRequest);
    Console.WriteLine("  Deleted Bob's cancelled notebook order.");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("  Delete skipped — condition not met.");
}

// Try to delete a non-cancelled order — should fail
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
    }
    .WithCondition(conditionBuilder,
        o => o.Status == "Cancelled");

    await client.DeleteItemAsync(deleteRequest2);
    Console.WriteLine("  Deleted Bob's keyboard order.");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("  Delete skipped — Bob's keyboard is not cancelled.");
}
```

### Scenario 7: Projection with Method Calls

Demonstrates method calls in selector expressions. The builder extracts the underlying properties — the methods themselves run client-side during result mapping.

```csharp
Console.WriteLine("\n=== Scenario 7: Projection with Method Calls ===");

// Enum.Parse, chained string methods, and plain property — all in one selector
var methodCallRequest = new GetItemRequest
{
    TableName = "Orders",
    Key = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = "CUSTOMER#alice" },
        ["SK"] = new AttributeValue { S = "ORDER#002" }
    }
}
.WithProjection(projectionBuilder,
    o => new { UpperName = o.Name.Trim().ToUpper(), o.Status, o.Quantity });

var methodCallResponse = await client.GetItemAsync(methodCallRequest);

// DynamoDB returns raw attributes — method calls run during mapping
Console.WriteLine($"  Name (raw): {methodCallResponse.Item["Name"].S}");
Console.WriteLine($"  Status: {methodCallResponse.Item["Status"].S}");
Console.WriteLine($"  Quantity: {methodCallResponse.Item["Quantity"].N}");
```

**Key point**: `o.Name.Trim().ToUpper()` in the selector doesn't affect what DynamoDB returns — the builder extracts `Name` for the projection. The `Trim().ToUpper()` transformation runs client-side when the compiled selector is invoked on the result.

### Scenario 9: Direct Result Mapping (Anonymous Type)

Demonstrates `IDirectResultMapper<TSource>.CreateMapper()` with an anonymous type projection. The mapper compiles once and runs at native speed.

```csharp
Console.WriteLine("\n=== Scenario 9: Result Mapping (Anonymous) ===");

var anonymousMapper = resultMapper.CreateMapper(o => new
{
    o.Name,
    o.Status,
    o.Quantity
});

// Re-fetch Alice's order (updated in Scenario 5)
var fetchRequest = new GetItemRequest
{
    TableName = "Orders",
    Key = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = "CUSTOMER#alice" },
        ["SK"] = new AttributeValue { S = "ORDER#002" }
    }
};

var fetchResponse = await client.GetItemAsync(fetchRequest);
var result = anonymousMapper(fetchResponse.Item);

Console.WriteLine($"  Name: {result.Name}");
Console.WriteLine($"  Status: {result.Status}");
Console.WriteLine($"  Quantity: {result.Quantity}");
```

### Scenario 10: Direct Result Mapping (Named DTO with Nested Path)

Demonstrates `CreateMapper()` with a named DTO (`OrderSummary`) including a nested property path (`o.ShippingAddress.City`).

```csharp
Console.WriteLine("\n=== Scenario 10: Result Mapping (Named DTO) ===");

var dtoMapper = resultMapper.CreateMapper(o => new OrderSummary
{
    OrderId = o.SK,
    CustomerName = o.Name,
    Status = o.Status,
    City = o.ShippingAddress.City
});

// Query Alice's orders and map each result
var aliceQuery = new QueryRequest { TableName = "Orders" }
    .WithKeyCondition(keyConditionBuilder,
        b => b.WithPartitionKey(o => o.PK, "CUSTOMER#alice")
              .WithSortKeyBeginsWith(o => o.SK, "ORDER#"));

var aliceResponse = await client.QueryAsync(aliceQuery);

Console.WriteLine($"  Alice's orders as OrderSummary DTOs:");
foreach (var item in aliceResponse.Items)
{
    var summary = dtoMapper(item);
    Console.WriteLine($"    - {summary.OrderId}: {summary.CustomerName} " +
                      $"[{summary.Status}] ({summary.City})");
}
```

## Expected Console Output

```
=== Scenario 1: Projection ===
  Name: Alice's Laptop
  Status: Shipped
  Quantity: 1

=== Scenario 2: Filter ===
  Found 2 shipped orders with quantity >= 1:
    - Alice's Laptop (Qty: 1)
    - Bob's Keyboard (Qty: 2)

=== Scenario 3: Filter Composition ===
  Found 3 shipped or delivered orders:
    - Alice's Laptop (Shipped)
    - Alice's Book (Delivered)
    - Bob's Keyboard (Shipped)

=== Scenario 4: Key Condition Query ===
  Alice's orders (3):
    - ORDER#001: Alice's Laptop
    - ORDER#002: Alice's Book
    - ORDER#003: Alice's Monitor

=== Scenario 5: Update ===
  Updated order:
    Status: Delivered
    Quantity: 2
    Notes: No notes provided
    Tags removed: True

=== Scenario 6: Conditional Delete ===
  Deleted Bob's cancelled notebook order.
  Delete skipped — Bob's keyboard is not cancelled.

=== Scenario 7: Projection with Method Calls ===
  Name (raw): Alice's Book
  Status: Delivered
  Quantity: 3

=== Scenario 9: Result Mapping (Anonymous) ===
  Name: Alice's Book
  Status: Delivered
  Quantity: 3

=== Scenario 10: Result Mapping (Named DTO) ===
  Alice's orders as OrderSummary DTOs:
    - ORDER#001: Alice's Laptop [Delivered] (Seattle)
    - ORDER#002: Alice's Book [Delivered] (Seattle)
    - ORDER#003: Alice's Monitor [Processing] (Portland)
```

**Note**: Scenario 5 mutates ORDER#001 (Status→Delivered, Quantity→2, Notes added, Tags removed), which affects subsequent output. Scenario 6 deletes ORDER#005, which affects query counts. The output above reflects the cumulative state after all mutations.

## Build & Run

```bash
# Start DynamoDB Local
docker compose up -d

# Run the demo
dotnet run

# Clean up
docker compose down
```

## README.md Contents

The `examples/ConsoleQuickStart/README.md` should contain:

```markdown
# Console Quick Start

The simplest possible demo of DynamoDb.ExpressionMapping — all code in a single
`Program.cs` with no dependency injection.

## What's Demonstrated

- Manual builder instantiation (no DI container)
- All 5 expression types: Projection, Filter, KeyCondition, Update, Condition
- Reserved keyword auto-aliasing (`Name`, `Status`)
- Custom type converter (`MoneyConverter`)
- Filter composition with `And()` / `Or()`
- Direct result mapping to anonymous types and named DTOs
- Nested property paths in result mapping
- Extension methods on AWS SDK request types

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later
- [Docker](https://www.docker.com/get-started)

## Run

    docker compose up -d
    dotnet run

## Clean Up

    docker compose down
```

## Key Patterns Demonstrated

| Pattern | How |
|---|---|
| Manual instantiation | `new ProjectionBuilder<Order>(factory, ...)` — no DI |
| All 5 expression types | Projection, Filter, KeyCondition, Update, Condition |
| Reserved keywords | `Name`, `Status` auto-aliased in projection and filter |
| Custom converter | `MoneyConverter` registered via `DynamoDbExpressionConfig.Builder.WithConverter()` |
| Filter composition | `FilterExpressionResult.Or(left, right)` with re-aliasing |
| Result mapping (anonymous) | `CreateMapper(o => new { o.Name, o.Status })` |
| Result mapping (named DTO) | `CreateMapper(o => new OrderSummary { ... })` |
| Nested property path | `o.ShippingAddress.City` in result mapper |
| Method calls in projection | `o.Name.Trim().ToUpper()` — builder extracts property, method runs client-side |
| Extension methods | `.WithProjection()`, `.WithFilter()`, `.WithKeyCondition()`, `.WithUpdate()`, `.WithCondition()` |
| ConditionalCheckFailed | Try/catch pattern for conditional delete |
