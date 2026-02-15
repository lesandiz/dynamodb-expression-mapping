# Implementation Plan

## Phase 0 — Project Scaffolding

- [x] Create solution file, class library project (`DynamoDb.ExpressionMapping`), and test project (`DynamoDb.ExpressionMapping.Tests`)
- [x] Configure `DynamoDb.ExpressionMapping.csproj`: `net8.0` TFM, root namespace, package metadata, dependencies (`AWSSDK.DynamoDBv2`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`)
- [x] Configure test project: xUnit, FluentAssertions, NSubstitute, Bogus, Testcontainers.DynamoDb
- [x] Create namespace folder structure: `Attributes/`, `Mapping/`, `Expressions/`, `ResultMapping/`, `ReservedKeywords/`, `Extensions/`, `Exceptions/`, `Caching/`

---

## Phase 1 — Foundation (no inter-dependencies)

These specs have zero dependencies on other specs. Build in any order; all three can be parallelized.

- [x] **Spec 14 — Exception hierarchy** (`Exceptions/`)
  - `ExpressionMappingException` abstract base and all concrete subtypes
  - Structured diagnostic properties on each exception type
  - Unit tests for construction, message formatting, inner-exception chaining

- [x] **Spec 08 — Reserved keyword handling** (`ReservedKeywords/`)
  - `ReservedKeywordRegistry` with 573+ words (frozen set)
  - `AliasGenerator` with scoped prefixes (`#proj_`, `#filt_`, `#cond_`, `#upd_`, `#key_` and value prefixes)
  - Unit tests for keyword detection, alias generation, prefix scoping

- [x] **Spec 09 — Expression caching** (`Caching/`)
  - `IExpressionCache` interface
  - `ExpressionKeyGenerator` (structural expression hashing)
  - `NullExpressionCache` (test bypass)
  - Default in-memory cache implementation
  - Unit tests for key generation determinism, cache hit/miss, null cache

---

## Phase 2 — Core Infrastructure

Depends on Phase 1 (exception hierarchy). These three specs can be parallelized.

- [x] **Spec 02 — Expression tree visitor** (`Expressions/`)
  - `PropertyPath` value object with `SegmentProperties` (`PropertyInfo[]`)
  - `ProjectionExpressionVisitor` — extracts paths from LINQ selectors
  - Supported patterns: member access, nested access, new anonymous/named types
  - Throws `UnsupportedExpressionException` for unsupported node types
  - Unit tests per Spec 12

- [x] **Spec 01 — Attribute name mapping** (`Mapping/`)
  - `[DynamoDbAttribute]` custom attribute
  - `IAttributeNameResolver<T>` interface + default implementation
  - `IAttributeNameResolverFactory` — cross-type resolution for nested paths
  - Resolution order: fluent overrides → `[DynamoDbAttribute]` → `[DynamoDBProperty]` → property name
  - Unit tests per Spec 12

- [x] **Spec 05 — Type converter system** (`Mapping/`)
  - `IAttributeValueConverter<T>` interface
  - Built-in converters: primitives, `DateTime`/`DateTimeOffset`, `Guid`, `byte[]`, collections, `Dictionary<string, AttributeValue>`
  - `AttributeValueConverterRegistry` — frozen default singleton, clone-to-customize
  - `ExpressionValueEmitter` — shared by all expression builders
  - Resolution order: `[DynamoDbConverter]` → exact → Nullable → Enum → open-generic → throw `MissingConverterException`
  - Unit tests per Spec 12

---

## Phase 3 — Expression Builders

Depends on Phases 1 + 2. Can be parallelized within this phase.

- [x] **Spec 03 — Projection expression builder** (`Expressions/`)
  - `ProjectionBuilder<TSource>` — selector → `ProjectionExpression` string
  - Uses `ProjectionExpressionVisitor` (Spec 02), `IAttributeNameResolverFactory` (Spec 01), `ReservedKeywordRegistry` + `AliasGenerator` (Spec 08), `IExpressionCache` (Spec 09)
  - Returns `ProjectionExpressionResult` with expression string + `ExpressionAttributeNames`
  - Unit tests per Spec 12

- [x] **Spec 04 — Direct result mapping** (`ResultMapping/`)
  - `IDirectResultMapper<TSource>` — `Dictionary<string, AttributeValue>` → `TResult`
  - Compiles mapping delegates via expression trees
  - Handles: anonymous types (constructor), named types (setters), records (parameterized ctor)
  - Uses converters (Spec 05) and attribute resolution (Spec 01)
  - Unit tests per Spec 12
  - **Status: 36/36 tests passing** — Full implementation complete:
    - `SinglePropertyMappingStrategy` for single-property mappers
    - `CompositeMappingStrategy` with expression compilation for multi-property scenarios
    - `DirectResultMapper<TSource>` with caching for compiled delegates
    - Array converter support (T[]) for all array types
    - Full test coverage including anonymous types, named types, records, and complex nested projections

- [x] **Spec 06 — Filter and condition expression builders** (`Expressions/`)
  - `FilterExpressionBuilder<TSource>` and `ConditionExpressionBuilder<TSource>`
  - Predicate → DynamoDB boolean expression string
  - Composability: `FilterExpressionResult.And()` / `Or()` with re-aliasing
  - Uses `ExpressionValueEmitter` (Spec 05), resolver factory (Spec 01), alias generator (Spec 08)
  - Unit tests per Spec 12
  - **Status: 108/108 tests passing** — Full implementation complete:
    - `FilterExpressionVisitor` for expression tree analysis
    - `FilterExpressionBuilder` / `ConditionExpressionBuilder` with scoped aliases (#filt_/:filt_v, #cond_/:cond_v)
    - `FilterExpressionResult` / `ConditionExpressionResult` with And/Or composition and re-aliasing
    - `DynamoDbFunctions` static class for DynamoDB-specific functions
    - Full test coverage including comparison ops, logical ops, string methods, null checks, DynamoDB functions, IN operator, nested properties, composition with re-aliasing

- [x] **Spec 07 — Update expression builder** (`Expressions/`)
  - `UpdateExpressionBuilder<TSource>` with fluent API
  - SET, REMOVE, ADD, DELETE clauses
  - Uses resolver factory (Spec 01), `ExpressionValueEmitter` (Spec 05), alias generator (Spec 08)
  - Unit tests per Spec 12
  - **Status: 20/20 tests passing** — Full implementation complete:
    - `UpdateExpressionBuilder<TSource>` with all fluent methods (Set, Increment, Decrement, SetIfNotExists, AppendToList, Remove, Add, Delete)
    - `UpdateExpressionResult` with scoped aliases (#upd_/:upd_v)
    - Conflict detection between SET/REMOVE/ADD/DELETE clauses
    - Full test coverage including all operations, nested properties, conflict validation, reserved keyword handling

- [x] **Spec 13 — Key condition expression builder** (`Expressions/`)
  - `KeyConditionExpressionBuilder<TSource>` with staged fluent API
  - Partition key equality + optional sort key operators (equals, comparison, between, begins_with)
  - Uses resolver factory (Spec 01), `ExpressionValueEmitter` (Spec 05), alias generator (Spec 08)
  - Unit tests per Spec 12
  - **Status: 24/24 tests passing** — Full implementation complete

---

## Phase 4 — Integration Layer

Depends on Phase 3.

- [x] **Spec 10 — AWS SDK request extensions** (`Extensions/`)
  - Extension methods on `GetItemRequest`, `QueryRequest`, `ScanRequest`, `BatchGetItemRequest`, `PutItemRequest`, `UpdateItemRequest`, `DeleteItemRequest`
  - Fluent chaining API
  - `RequestMergeHelpers` — safe dictionary merging, throws `ExpressionAttributeConflictException` on collision
  - Unit tests per Spec 12
  - **Status: 35/35 tests passing** — Full implementation complete:
    - `RequestMergeHelpers` for safe attribute name/value merging with conflict detection
    - `ProjectionExtensions` for GetItem, Query, Scan, BatchGetItem requests
    - `FilterExtensions` for Query and Scan filter expressions
    - `ConditionExtensions` for Put, Delete, Update condition expressions
    - `KeyConditionExtensions` for Query key conditions with staged fluent API
    - `UpdateExtensions` for UpdateItem update expressions
    - `CombinedExtensions` for projection + filter in one call
    - Full test coverage including happy paths, error cases, null checks, fluent chaining, and alias scope isolation

- [ ] **Spec 11 — Configuration and dependency injection** (root + `Extensions/`)
  - `DynamoDbExpressionConfig` builder pattern
  - `IServiceCollection.AddDynamoDbExpressionMapping()` registration extension
  - Per-entity fluent configuration
  - Manual instantiation fallback (no-DI path)
  - Unit tests per Spec 12

---

## Phase 5 — Integration Tests

Depends on all prior phases.

- [ ] **Spec 12 — Integration test suite**
  - `DynamoDbFixture` with Testcontainers.DynamoDb (`IAsyncLifetime`)
  - `[Collection("DynamoDb")]` shared fixture, per-test-class table lifecycle
  - End-to-end scenarios: build expression → execute against DynamoDB Local → verify results
  - Round-trip tests for all expression types
  - Edge cases: deeply nested paths, reserved keywords in real queries, large batch operations

---

## Dependency Graph (quick reference)

```
Phase 0  Scaffolding
           │
Phase 1  ┌─┴──────────────────┐
         │  Spec 14 (exceptions)
         │  Spec 08 (keywords)
         │  Spec 09 (caching)
         └─┬──────────────────┘
           │
Phase 2  ┌─┴──────────────────┐
         │  Spec 02 (visitor)
         │  Spec 01 (mapping)
         │  Spec 05 (converters)
         └─┬──────────────────┘
           │
Phase 3  ┌─┴──────────────────┐
         │  Spec 03 (projection)
         │  Spec 04 (result map)
         │  Spec 06 (filter/cond)
         │  Spec 07 (update)
         │  Spec 13 (key cond)
         └─┬──────────────────┘
           │
Phase 4  ┌─┴──────────────────┐
         │  Spec 10 (SDK ext)
         │  Spec 11 (config/DI)
         └─┬──────────────────┘
           │
Phase 5    Spec 12 (integration tests)
```
