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
dotnet test
dotnet test --filter "Category!=Integration"        # unit tests only
dotnet test --filter "Category=Integration"          # integration tests only (requires Docker for DynamoDB Local)
```

## Test Framework & Conventions

- **xUnit** test framework, **FluentAssertions**, **NSubstitute**, **Bogus**, **Testcontainers.DynamoDb**
- Test project: `DynamoDb.ExpressionMapping.Tests/`
- Integration tests use `[Collection("DynamoDb")]` with a shared `DynamoDbFixture` (`IAsyncLifetime`) via Testcontainers — each test class creates/deletes its own table
- Spec 12 contains the complete test plan with every test case listed

## Dependencies

- `AWSSDK.DynamoDBv2` (>= 3.7.x)
- `Microsoft.Extensions.Logging.Abstractions` (>= 8.0.0) — optional
- `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 8.0.0) — optional

## Package Namespace

Root namespace: `DynamoDb.ExpressionMapping`

Sub-namespaces: `Attributes`, `Mapping`, `Expressions`, `ResultMapping`, `ReservedKeywords`, `Extensions`, `Exceptions`, `Caching`
