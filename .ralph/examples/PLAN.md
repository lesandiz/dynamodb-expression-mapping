# Examples — Implementation Plan

Console Quick Start first (validates library API surface with minimal moving parts), then Web API.

---

## Phase 0 — Project Scaffolding ✓

- [x] 0.1 Create `examples/ConsoleQuickStart/ConsoleQuickStart.csproj` targeting `net8.0` with project reference to `src/DynamoDb.ExpressionMapping/` and `AWSSDK.DynamoDBv2` package reference
  - Created with project reference to library and AWSSDK.DynamoDBv2 package reference
  - Added placeholder Program.cs - builds successfully
- [x] 0.2 Create `examples/WebApiExample/WebApiExample.csproj` targeting `net8.0` with project reference to `src/DynamoDb.ExpressionMapping/`, `AWSSDK.DynamoDBv2`, and ASP.NET package references (`Swashbuckle.AspNetCore`)
  - Created with project reference to library, AWSSDK.DynamoDBv2, and Swashbuckle.AspNetCore
  - Added minimal Program.cs - builds successfully
- [x] 0.3 Verify both projects build against the library (`dotnet build`)
  - Both ConsoleQuickStart and WebApiExample build without errors

---

## Phase 1 — Console Quick Start (Spec 01)

Priority order follows dependency chain: models → converter → infra → setup → scenarios.

- [x] 1.1 Create entity models: `Order`, `Address`, `Money`, `OrderSummary` (Spec 01 §Entity Models)
  - Created Models.cs with all four classes matching Spec 01 requirements
- [x] 1.2 Create `MoneyConverter` implementing `IAttributeValueConverter<Money>` (Spec 01 §Custom Converter)
  - Created MoneyConverter.cs with IAttributeValueConverter<Money> implementation - builds successfully
- [x] 1.3 Create `docker-compose.yml` for DynamoDB Local (Spec 01 §Infrastructure)
  - Created docker-compose.yml with DynamoDB Local container configuration per spec
- [x] 1.4 `Program.cs` — DynamoDB client setup, table creation (idempotent), seed 5 orders (Spec 01 §Manual Instantiation, §Seed Data)
  - DynamoDB client setup complete (using port 8002 due to port 8000 conflict)
  - Table creation implemented with idempotent ResourceInUseException handling
  - 5 orders seeded across 2 customers (Alice: 3 orders, Bob: 2 orders)
  - Successfully tested - table created and data verified
- [x] 1.5 `Program.cs` — Manual builder instantiation via `DynamoDbExpressionConfig.Builder` and `AttributeNameResolverFactory` (Spec 01 §Manual Instantiation)
  - Added config builder with MoneyConverter registration
  - Created AttributeNameResolverFactory for type resolution
  - Instantiated all 6 builders: ProjectionBuilder, FilterExpressionBuilder, ConditionExpressionBuilder, UpdateExpressionBuilder, KeyConditionExpressionBuilder, DirectResultMapper
  - All builders ready for use in scenarios
- [x] 1.6 Scenario 1: Projection with reserved keywords (Spec 01 §Scenario 1)
  - Projection working correctly - reserved keywords (`status`, `data`, `name`) properly aliased with `#proj_` prefix
  - Direct result mapping successfully returns `OrderSummary` with all fields populated
  - Console output matches expected format from Spec 01
- [x] 1.7 Scenario 2: Filter expression (Spec 01 §Scenario 2)
  - Filter expression working correctly - filters orders with Status == "Shipped" AND Quantity >= 1
  - Reserved keyword "Status" properly aliased with `#filt_` prefix
  - Direct result mapping successfully returns filtered `OrderSummary` results
  - Console output shows 2 shipped orders (Alice's Laptop and Bob's Keyboard)
- [x] 1.8 Scenario 3: Filter composition with `And()`/`Or()` (Spec 01 §Scenario 3)
  - Filter composition working correctly - Or() method properly re-aliases independent filters to prevent collisions
  - Combined filter (Status == "Shipped" OR Status == "Delivered") returns expected 3 orders
  - Console output shows 2 Shipped orders (Alice's Laptop, Bob's Keyboard) + 1 Delivered order (Alice's Mouse)
- [x] 1.9 Scenario 4: Key condition query with `begins_with` (Spec 01 §Scenario 4)
- [x] 1.10 Scenario 5: Update expression — SET, Increment, SetIfNotExists, Remove (Spec 01 §Scenario 5)
  - UpdateExpressionBuilder working correctly - demonstrates 4 operation types in single expression
  - SET changes Status to "Delivered", Increment raises Quantity from 1 to 2
  - SetIfNotExists sets Notes to "No notes provided" (attribute didn't exist)
  - Remove deletes Tags attribute entirely
  - Console output matches expected format from Spec 01
- [ ] 1.11 Scenario 6: Conditional delete with `ConditionalCheckFailedException` handling (Spec 01 §Scenario 6)
- [ ] 1.12 Scenario 7: Direct result mapping — anonymous type (Spec 01 §Scenario 7)
- [ ] 1.13 Scenario 8: Direct result mapping — named DTO with nested path (Spec 01 §Scenario 8)
- [ ] 1.14 Create `examples/ConsoleQuickStart/README.md` (Spec 01 §README.md Contents)
- [ ] 1.15 End-to-end verification: `docker compose up -d && dotnet run`, compare output to Spec 01 §Expected Console Output

---

## Phase 2 — Web API Example (Spec 02)

Priority order: models/DTOs → converter → infra → DI → repository → controllers → verification.

### 2A — Models, DTOs, Converter

- [ ] 2.1 Create entity models: `Order`, `Product`, `Customer`, `Address`, `Money` with `[DynamoDbAttribute]` annotations (Spec 02 §Entity Models)
- [ ] 2.2 Create DTOs: `OrderDto`, `OrderDetailDto`, `CreateOrderRequest`, `UpdateOrderRequest`, `ProductDto`, `CustomerDto`, `PagedResponse<T>` (Spec 02 §DTOs)
- [ ] 2.3 Create `MoneyConverter` (Spec 02 §Custom Converter)

### 2B — Infrastructure

- [ ] 2.4 Create `docker-compose.yml` with DynamoDB Local + API container (Spec 02 §Infrastructure)
- [ ] 2.5 Create `Dockerfile` — multi-stage .NET build (Spec 02 §Infrastructure)
- [ ] 2.6 Create `DynamoDbSeeder` (`IHostedService`) — table creation + seed data (Spec 02 §Infrastructure, §DynamoDbSeeder)
- [ ] 2.7 Create `TableDefinitions` — single-table schema with PK/SK (Spec 02 §Single-Table Design)

### 2C — DI & Repository

- [ ] 2.8 `Program.cs` — DI setup: `AddDynamoDbExpressionMapping()`, `AddDynamoDbEntity<T>()`, Swagger, DynamoDB client (Spec 02 §DI Setup)
- [ ] 2.9 Create `IOrderRepository` interface (Spec 02 §Repository Pattern)
- [ ] 2.10 Create `OrderRepository` — inject all typed builders, implement all methods (Spec 02 §Repository Pattern, §REST Endpoints)
- [ ] 2.11 Pagination helpers: `EncodePaginationToken` / `DecodePaginationToken` (Spec 02 §Pagination)

### 2D — Controllers

- [ ] 2.12 `OrdersController` — GET (list), GET (single), POST, PUT, DELETE (Spec 02 §REST Endpoints)
- [ ] 2.13 `ProductsController` — GET with dynamic filter composition (Spec 02 §GET /api/products)
- [ ] 2.14 `CustomersController` — GET with projection + nested path (Spec 02 §GET /api/customers/{id})

### 2E — Verification

- [ ] 2.15 Create `examples/WebApiExample/README.md` (Spec 02 §README.md Contents)
- [ ] 2.16 Build verification: `dotnet build`
- [ ] 2.17 Start services: `docker compose up --build`, wait for healthy
- [ ] 2.18 Verify `GET /api/orders?customerId=alice` — returns seeded orders with pagination shape (Spec 02 §Query Orders)
- [ ] 2.19 Verify `GET /api/orders/alice/001` — returns full order detail with nested `city` field (Spec 02 §Get Single Order)
- [ ] 2.20 Verify `POST /api/orders` — creates new order, second POST with same keys returns 409 (Spec 02 §Create Order)
- [ ] 2.21 Verify `PUT /api/orders/alice/001` — partial update changes only specified fields (Spec 02 §Update Order)
- [ ] 2.22 Verify `DELETE /api/orders/bob/005` — succeeds, second DELETE returns 404 (Spec 02 §Delete Order)
- [ ] 2.23 Verify `GET /api/products?category=Electronics&activeOnly=true` — returns filtered products (Spec 02 §Get Products)
- [ ] 2.24 Verify `GET /api/customers/alice` — returns customer with nested `city` (Spec 02 §Get Customer)
- [ ] 2.25 Verify pagination: `GET /api/orders?customerId=alice&limit=2` — returns `nextToken`, second call with token returns remaining items

---

## Dependency Graph

```
Phase 0  Scaffolding (both projects)
           │
Phase 1    Console Quick Start (Spec 01)
           │  1.1–1.3  Models, converter, docker
           │  1.4–1.5  Client setup, builders
           │  1.6–1.13 Scenarios (sequential, mutations affect later output)
           │  1.14–1.15 README, verify
           │
Phase 2    Web API Example (Spec 02)
           │  2A  Models, DTOs, converter
           │  2B  Infrastructure (docker, Dockerfile, seeder)
           │  2C  DI, repository, pagination
           │  2D  Controllers
           │  2E  README, build, per-endpoint verification
```
