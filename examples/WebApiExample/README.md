# Web API Example

Demonstrates `DynamoDb.ExpressionMapping` in a production-like ASP.NET Core Web API using the repository pattern, dependency injection, single-table design, and Swagger/OpenAPI documentation.

## What's Demonstrated

- ✅ **Full DI registration** — `AddDynamoDbExpressionMapping()` + `AddDynamoDbEntity<T>()`
- ✅ **Repository pattern** — `IOrderRepository` with typed builder injection
- ✅ **Single-table DynamoDB design** — `PK/SK` patterns for multiple entity types (customers, orders, products)
- ✅ **Dynamic filter composition** — `And()`/`Or()` with automatic re-aliasing
- ✅ **Partial updates** — `UpdateExpressionBuilder` with conditional field inclusion
- ✅ **Condition expressions** — Create guards (`attribute_not_exists`), delete guards (`attribute_exists`)
- ✅ **Token-based pagination** — `LastEvaluatedKey` encoded as Base64 JSON
- ✅ **Direct result mapping to DTOs** — Anonymous types + named DTOs + nested paths
- ✅ **Custom attribute mapping** — `[DynamoDbAttribute("status")]` on entity models
- ✅ **Custom type converter** — `MoneyConverter` for `Money` value object
- ✅ **Swagger/OpenAPI** — Interactive API documentation at `/swagger`
- ✅ **Dockerized deployment** — Multi-stage Dockerfile + Docker Compose

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://docs.docker.com/get-docker/) (for DynamoDB Local)

## Run (Docker)

```bash
docker compose up --build
```

**Endpoints:**
- API: http://localhost:5000
- Swagger UI: http://localhost:5000/swagger
- DynamoDB Local: http://localhost:8000

The `DynamoDbSeeder` hosted service will automatically:
1. Create the `AppData` table if it doesn't exist
2. Seed customers, orders, and products if the table is empty
3. Wait for the table to become active before seeding

## Run (Local)

```bash
# Terminal 1 — Start DynamoDB Local
docker compose up dynamodb

# Terminal 2 — Run the API
cd examples/WebApiExample
dotnet run
```

API will be available at http://localhost:5000 (or the port shown in the console output).

## API Endpoints

| Method | Endpoint                                 | Description                                      |
| ------ | ---------------------------------------- | ------------------------------------------------ |
| GET    | `/api/orders`                            | Query orders with filters (`customerId`, `status`) and pagination |
| GET    | `/api/orders/{customerId}/{orderId}`     | Get single order with nested `ShippingAddress.City` |
| POST   | `/api/orders`                            | Create order with condition guard (409 on duplicate) |
| PUT    | `/api/orders/{customerId}/{orderId}`     | Partial update (only specified fields)           |
| DELETE | `/api/orders/{customerId}/{orderId}`     | Delete order with condition guard (404 if not found) |
| GET    | `/api/products`                          | Get products with dynamic filters (`category`, `activeOnly`) |
| GET    | `/api/customers/{id}`                    | Get customer with nested `Address.City`          |

### Example Requests

**Query Alice's orders:**
```bash
curl http://localhost:5000/api/orders?customerId=alice
```

**Get specific order:**
```bash
curl http://localhost:5000/api/orders/alice/001
```

**Create new order:**
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "alice",
    "orderId": "999",
    "productId": "laptop",
    "quantity": 1,
    "totalAmount": { "amount": 1200.00, "currency": "USD" },
    "status": "Pending"
  }'
```

**Partial update:**
```bash
curl -X PUT http://localhost:5000/api/orders/alice/001 \
  -H "Content-Type: application/json" \
  -d '{ "status": "Shipped", "quantity": 2 }'
```

**Delete order:**
```bash
curl -X DELETE http://localhost:5000/api/orders/alice/999
```

**Filter products by category:**
```bash
curl http://localhost:5000/api/products?category=Electronics&activeOnly=true
```

**Get customer with nested city:**
```bash
curl http://localhost:5000/api/customers/alice
```

**Pagination example:**
```bash
# First page (limit 2)
curl http://localhost:5000/api/orders?customerId=alice&limit=2

# Next page (use nextToken from previous response)
curl "http://localhost:5000/api/orders?customerId=alice&limit=2&nextToken=<token>"
```

## Key Patterns Demonstrated

| Pattern                     | Location                          | Description                                      |
| --------------------------- | --------------------------------- | ------------------------------------------------ |
| **Single-table design**     | `TableDefinitions.cs`             | PK/SK patterns for multiple entity types         |
| **Repository pattern**      | `OrderRepository.cs`              | Encapsulates DynamoDB logic, injects typed builders |
| **DI registration**         | `Program.cs`                      | `AddDynamoDbExpressionMapping()` + `AddDynamoDbEntity<T>()` |
| **Dynamic filter composition** | `ProductsController.cs`         | `And()` method with automatic re-aliasing        |
| **Partial updates**         | `OrderRepository.UpdateOrderAsync` | Conditional `SET` based on request fields        |
| **Condition guards**        | `OrderRepository.CreateOrderAsync` | `attribute_not_exists(PK)` prevents overwrites   |
| **Pagination**              | `OrderRepository.QueryOrdersAsync` | `LastEvaluatedKey` → Base64 JSON token           |
| **Direct result mapping**   | All repository methods            | Maps to DTOs without full entity hydration       |
| **Custom converter**        | `MoneyConverter.cs`               | Converts `Money` to/from `AttributeValue`        |
| **Nested path projection**  | `OrderRepository.GetOrderAsync`   | `ShippingAddress.City` → `city` in `OrderDetailDto` |

## Feature Coverage vs Console Example

| Feature                      | Console (Spec 01) | Web API (Spec 02) |
| ---------------------------- | ----------------- | ----------------- |
| Manual builder instantiation | ✅                | ❌                |
| DI registration              | ❌                | ✅                |
| Repository pattern           | ❌                | ✅                |
| Single-table design          | ❌                | ✅                |
| Anonymous type mapping       | ✅                | ❌                |
| Named DTO mapping            | ✅                | ✅                |
| Pagination                   | ❌                | ✅                |
| Swagger/OpenAPI              | ❌                | ✅                |
| Dockerized deployment        | ❌                | ✅                |
| Filter composition           | ✅                | ✅                |
| Update expression            | ✅                | ✅                |
| Condition expression         | ✅                | ✅                |
| Reserved keyword handling    | ✅                | ✅                |
| Custom converter             | ✅                | ✅                |

## Clean Up

```bash
docker compose down
```

To remove volumes (including DynamoDB Local data):
```bash
docker compose down -v
```
