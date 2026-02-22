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
dotnet test --filter "Category=Property"                          # property tests only (100 iterations default)
dotnet test --filter "Category!=Property"                         # unit tests only, skip property tests
```

**Property-based tests**: Default is 100 iterations per property (fast local feedback). CI sets `FSCHECK_MAX_TEST=10000` for full validation. All property test classes are tagged with `[Trait("Category", "Property")]` for filtering.

**Note:** On Windows, `FSCHECK_MAX_TEST=100 dotnet test` does **not** propagate the env var to the .NET test host. The default of 100 in `PropertyTestConfig.cs` handles the local case; CI pipelines set the env var natively.

## Verification Strategy

Tiered testing to maintain a fast feedback loop while ensuring affected code is always verified before committing.

### Pre-commit (must pass before every commit)
Run affected projects, **excluding property tests** (they are slow even at low iteration counts due to per-iteration expression tree generation cost). Target: **under 1 minute**.

```bash
# Unit tests (excludes property tests)
dotnet test tests/DynamoDb.ExpressionMapping.Tests/ --filter "Category!=Property"

# Integration tests (when integration code is affected)
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/
```

**Rule:** If code was moved, refactored, or its project references changed, the affected tests must be **executed** — a successful build is not verification. Do not defer to CI.

### Pre-complete (must pass before marking a PLAN.md item done)
Full test suite including property tests. This is the gate before marking work complete.

```bash
dotnet test tests/DynamoDb.ExpressionMapping.Tests/              # all unit + property tests
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/   # integration tests
```

### CI-only (not required locally)
High-iteration property tests and mutation testing. CI sets `FSCHECK_MAX_TEST=10000` natively.

```bash
dotnet test --filter "Category=Property"  # CI sets FSCHECK_MAX_TEST=10000
dotnet stryker
```


## Test Framework & Conventions

- **xUnit** test framework, **FluentAssertions**, **NSubstitute**, **Bogus**, **Testcontainers.DynamoDb**
- Unit/property test project: `tests/DynamoDb.ExpressionMapping.Tests/` — no Docker dependency
- Integration test project: `tests/DynamoDb.ExpressionMapping.IntegrationTests/` — uses `[Collection("DynamoDb")]` with a shared `DynamoDbFixture` (`IAsyncLifetime`) via Testcontainers; each test class creates/deletes its own table
- Integration tests are in a separate project to prevent xUnit's eager collection fixture initialization from triggering Docker container startup during unit-only test runs (including Stryker mutation testing and coverage collection)
- The integration test project references the unit test project for shared fixtures; the main library flows in as a transitive dependency (no explicit `ProjectReference` to `DynamoDb.ExpressionMapping.csproj` needed)
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
