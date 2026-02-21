# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

`DynamoDb.ExpressionMapping` — a .NET 8.0+ library that converts C# LINQ expression trees into DynamoDB expression strings (`ProjectionExpression`, `FilterExpression`, `ConditionExpression`, `UpdateExpression`, `KeyConditionExpression`) with direct result mapping that avoids full entity hydration.

This is a **spec-first project**. All 15 specs live in `.ralph/core/specs/` and define the complete architecture, API surface, and test plan. No source code exists yet — implementation should follow the specs precisely.

## Architecture

The library has six major subsystems, each corresponding to one or more specs:

### Expression Builders (Specs 02, 03, 06, 07, 13)
- `ProjectionExpressionVisitor` extracts `PropertyPath` objects from expression trees (Spec 02)
- `ProjectionBuilder<TSource>` builds `ProjectionExpression` strings from selectors (Spec 03)
- `FilterExpressionBuilder<TSource>` / `ConditionExpressionBuilder<TSource>` build boolean expressions from predicates (Spec 06)
- `UpdateExpressionBuilder<TSource>` builds update expressions via fluent API (Spec 07)
- `KeyConditionExpressionBuilder<TSource>` builds key conditions via staged fluent API (Spec 13)

### Attribute Name Resolution (Spec 01)
- `IAttributeNameResolver<T>` resolves C# property names to DynamoDB attribute names
- `IAttributeNameResolverFactory` provides cross-type resolution for nested paths (e.g., `p.Address.City` resolves `Address` against `Order`, `City` against `Address`)
- Resolution order: fluent overrides → `[DynamoDbAttribute]` → `[DynamoDBProperty]` (AWS SDK) → property name

### Type Converter System (Spec 05)
- `IAttributeValueConverter<T>` converts between .NET types and `AttributeValue`
- `AttributeValueConverterRegistry` with frozen default singleton + clone-to-customize pattern
- `ExpressionValueEmitter` is the shared component used by all expression builders for value conversion
- Resolution order: `[DynamoDbConverter]` on property → registry exact → Nullable wrapper → Enum → open-generic collection → throw

### Direct Result Mapping (Spec 04)
- `IDirectResultMapper<TSource>` maps `Dictionary<string, AttributeValue>` directly to `TResult` without full entity hydration
- Compiles mapping delegates via expression trees — runs at native speed after initial compilation
- Handles anonymous types (constructor), named types (property setters), records (parameterized constructor)

### Reserved Keywords & Alias Scoping (Spec 08)
- Each expression type gets a scoped alias prefix to prevent collisions:
  - Projection: `#proj_`, Filter: `#filt_`/`:filt_v`, Condition: `#cond_`/`:cond_v`, Update: `#upd_`/`:upd_v`, KeyCondition: `#key_`/`:key_v`
- `FilterExpressionResult.And()`/`Or()` use re-aliasing to safely compose independently built filters

### AWS SDK Integration (Spec 10)
- Extension methods on `GetItemRequest`, `QueryRequest`, `ScanRequest`, `BatchGetItemRequest`, `PutItemRequest`, `UpdateItemRequest`, `DeleteItemRequest`
- `RequestMergeHelpers` handles dictionary merging with collision detection

## Key Design Decisions

- **Generic-first**: All public APIs are generic over `TSource` (entity type), not coupled to specific models
- **`PropertyPath.SegmentProperties`**: Carries `PropertyInfo` for every segment, eliminating additional reflection during attribute name resolution and converter selection
- **Factory pattern for resolvers**: `IAttributeNameResolverFactory` auto-discovers types via reflection; only types deviating from convention need explicit configuration
- **Fail-fast**: All validation errors throw at expression-build time, never during query execution
- **Exception hierarchy**: `ExpressionMappingException` (abstract base) → `UnsupportedExpressionException`, `MissingConverterException`, `ExpressionAttributeConflictException`, `InvalidExpressionException` (abstract) → `InvalidProjection/Filter/Update/KeyCondition`

## Build & Test Commands

```bash
dotnet build
dotnet test tests/DynamoDb.ExpressionMapping.Tests/              # unit + property tests (no Docker required)
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/   # integration tests only (requires Docker for DynamoDB Local)
FSCHECK_MAX_TEST=100 dotnet test --filter "Category=Property"    # property tests, fast (100 iterations)
FSCHECK_MAX_TEST=10000 dotnet test --filter "Category=Property"  # property tests, full validation (10k iterations)
```

**Property-based tests**: Default is 1,000 iterations per property. Use `FSCHECK_MAX_TEST=100` for rapid development/agent workflows. Use `FSCHECK_MAX_TEST=10000` for full validation before completing Phase 1.

**Expected durations** (do not kill the process prematurely):
- `FSCHECK_MAX_TEST=100`: ~30 seconds
- `FSCHECK_MAX_TEST=1000` (default): ~2-3 minutes — use a 5-minute timeout
- `FSCHECK_MAX_TEST=10000`: ~10-15 minutes — use a 20-minute timeout (Bash `"timeout": 600000`)

## Test Framework & Conventions

- **xUnit** test framework, **FluentAssertions**, **NSubstitute**, **Bogus**, **Testcontainers.DynamoDb**
- Unit/property test project: `tests/DynamoDb.ExpressionMapping.Tests/` — no Docker dependency
- Integration test project: `tests/DynamoDb.ExpressionMapping.IntegrationTests/` — uses `[Collection("DynamoDb")]` with a shared `DynamoDbFixture` (`IAsyncLifetime`) via Testcontainers; each test class creates/deletes its own table
- Integration tests are in a separate project to prevent xUnit's eager collection fixture initialization from triggering Docker container startup during unit-only test runs (including Stryker mutation testing and coverage collection)
- Spec 12 contains the complete test plan with every test case listed

## Dependencies

- `AWSSDK.DynamoDBv2` (>= 3.7.x)
- `Microsoft.Extensions.Logging.Abstractions` (>= 8.0.0) — optional
- `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 8.0.0) — optional

## Package Namespace

Root namespace: `DynamoDb.ExpressionMapping`

Sub-namespaces: `Attributes`, `Mapping`, `Expressions`, `ResultMapping`, `ReservedKeywords`, `Extensions`, `Exceptions`, `Caching`

## Examples

### ConsoleQuickStart

Example project demonstrating library usage. DynamoDB Local runs on **port 8002** (port 8000 was in use).

To run:
```bash
cd examples/ConsoleQuickStart && docker compose up -d && dotnet run
```

### WebApiExample

ASP.NET Core Web API demonstrating real-world usage with RESTful endpoints. DynamoDB Local runs on **port 8003** (host) mapping to 8000 (container).

To run:
```bash
cd examples/WebApiExample && docker compose up -d
# API runs on port 5000
# Swagger UI: http://localhost:5000/swagger/index.html
```

Test endpoints:
```bash
# Query orders with pagination
curl "http://localhost:5000/api/orders?customerId=alice&limit=2"

# Get single order
curl "http://localhost:5000/api/orders/alice/001"

# Create order
curl -X POST http://localhost:5000/api/orders -H "Content-Type: application/json" \
  -d '{"orderId":"ORD001","customerId":"CUST001","name":"Test","status":"Processing","quantity":1,"totalAmount":99.99,"totalCurrency":"USD","street":"123 Main","city":"Portland","postCode":"12345","tags":["test"]}'

# Update order
curl -X PUT http://localhost:5000/api/orders/CUST001/ORD001 -H "Content-Type: application/json" \
  -d '{"status":"Shipped","notes":"Shipped via FedEx"}'

# Delete order
curl -X DELETE http://localhost:5000/api/orders/CUST001/ORD001

# Filter products
curl "http://localhost:5000/api/products?category=Electronics&activeOnly=true"

# Get customer
curl "http://localhost:5000/api/customers/alice"
```

### SoakTests

Soak and concurrency testing harness. DynamoDB Local runs on **port 8004** (host) mapping to 8000 (container).

To run:
```bash
cd tests/DynamoDb.ExpressionMapping.SoakTests && docker compose up -d
# Run soak tests: dotnet run -- --duration 10 --concurrency 8
```
