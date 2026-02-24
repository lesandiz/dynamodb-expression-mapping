# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.2] - 2026-02-24

### Added

#### Property-Based Testing
- **FsCheck Integration** — 100-iteration property-based tests across all expression builders, type converters, and composability
- **KeyConditionOperationGenerator** — Custom FsCheck generator for property-based testing parity on key conditions

#### Soak & Concurrency Testing
- **Soak Test Suite** — 15-minute soak test validated 2.3M operations with 0 failures at 2,100+ ops/sec
- **Concurrency Scenarios** — 5 concurrency scenarios validating thread safety across shared builder instances

#### Mutation Testing
- **Stryker.NET Integration** — Mutation testing achieving 90.8% overall score (91.4% on expression builders) with 307 mutation-killing tests

#### Snapshot Testing
- **Verify Integration** — 30 snapshot tests locking all expression output formats with alias scope isolation

#### Benchmarks
- **BenchmarkDotNet Suite** — 9 benchmark classes covering projection, filter, filter composition, update, direct result mapper, type converter, expression cache, key condition, and end-to-end scenarios
- **Baseline Snapshots** — Committed baseline JSON results (warm paths 1.4x–4.8x faster than cold)

#### Code Coverage Enforcement
- **Coverlet + ReportGenerator** — 95.37% line / 85.81% branch coverage with CI threshold enforcement (90% line / 85% branch)
- **CI Coverage Reporting** — Automated coverage PR comments via ReportGenerator

#### API Compatibility Tracking
- **PublicApiAnalyzers** — `PublicAPI.Shipped.txt` tracking 303 public API declarations with breaking-change detection in release pipeline

### Changed

#### Test Infrastructure
- **Isolated Integration Tests** — Separated integration tests into dedicated project (`DynamoDb.ExpressionMapping.IntegrationTests`) to prevent Docker startup during unit-only runs
- **Test Suite Refactoring** — ~123 fewer test methods with identical execution count through consolidation
- **Test Quality Audit** — Fixed 45 issues, deleted 23 redundant tests, fixed `Gen.Where` crash, cached dynamic Regex instances

### Fixed
- Fixed incorrect integration test assertion for key condition aliases
- Fixed CI workflow to target integration test project explicitly for build
- Added `pull-requests: write` permission for CI PR comments

## [0.1.1] - 2026-02-15

### Added

#### Core Infrastructure
- **Attribute Name Mapping** — `IAttributeNameResolver<T>` with configurable resolution order: fluent overrides → `[DynamoDbAttribute]` → `[DynamoDBProperty]` → convention
- **Type Converter System** — `IAttributeValueConverter<T>` with extensible registry and built-in converters for primitives, dates, collections, enums, and nullable types
- **Expression Tree Visitor** — `ProjectionExpressionVisitor` extracts `PropertyPath` objects with `SegmentProperties` from LINQ selectors
- **Direct Result Mapping** — `IDirectResultMapper<TSource>` compiles mapping delegates for zero-allocation result hydration

#### Expression Builders
- **ProjectionBuilder** — Converts selectors to `ProjectionExpression` strings with automatic reserved keyword aliasing
- **FilterExpressionBuilder** — Builds `FilterExpression` from predicates with support for comparison operators, boolean logic, string operations, null checks, and IN operator
- **ConditionExpressionBuilder** — Builds `ConditionExpression` for conditional writes (Put, Delete, Update)
- **UpdateExpressionBuilder** — Fluent API for `UpdateExpression` with SET, REMOVE, ADD, DELETE clauses and operations like Increment, Decrement, SetIfNotExists, AppendToList
- **KeyConditionExpressionBuilder** — Staged fluent API for `KeyConditionExpression` with partition key equality and sort key operators (equality, comparison, BETWEEN, begins_with)

#### AWS SDK Integration
- **Request Extensions** — Fluent extension methods for `GetItemRequest`, `QueryRequest`, `ScanRequest`, `BatchGetItemRequest`, `PutItemRequest`, `UpdateItemRequest`, `DeleteItemRequest`
- **Expression Composition** — Safe dictionary merging with `ExpressionAttributeConflictException` on alias collision
- **Filter Composition** — `FilterExpressionResult.And()` and `.Or()` with automatic re-aliasing

#### Configuration & DI
- **DynamoDbExpressionConfig** — Builder pattern for global configuration including null handling modes
- **Dependency Injection** — `IServiceCollection.AddDynamoDbExpressionMapping()` with per-entity configuration support via `AddDynamoDbEntity<TEntity>()`
- **Manual Instantiation** — Fallback for non-DI scenarios

#### Performance & Caching
- **Expression Caching** — `IExpressionCache` with structural expression hashing via `ExpressionKeyGenerator`
- **Compiled Delegates** — Result mappers compile to native-speed delegates after initial analysis
- **Null Expression Cache** — `NullExpressionCache` for test scenarios

#### Reserved Keywords & Aliasing
- **Reserved Keyword Registry** — 573+ DynamoDB reserved keywords with automatic detection
- **Scoped Alias Prefixes** — Separate namespaces prevent collisions: `#proj_`, `#filt_/:filt_v`, `#cond_/:cond_v`, `#upd_/:upd_v`, `#key_/:key_v`

#### Exception Hierarchy
- **ExpressionMappingException** — Abstract base for all library exceptions
- **UnsupportedExpressionException** — Unsupported expression tree patterns
- **MissingConverterException** — No converter found for .NET type
- **ExpressionAttributeConflictException** — Alias collision during expression merge
- **InvalidExpressionException** — Abstract base with concrete subtypes: `InvalidProjectionException`, `InvalidFilterException`, `InvalidUpdateException`, `InvalidKeyConditionException`

#### Attributes
- **[DynamoDbAttribute]** — Specify custom DynamoDB attribute name
- **[DynamoDbIgnore]** — Exclude property from mapping
- **[DynamoDbConverter]** — Specify custom converter for property

#### Examples
- **ConsoleQuickStart** — Console app demonstrating basic library usage with DynamoDB Local
- **WebApiExample** — ASP.NET Core Web API with repository pattern, Swagger, and multi-table operations

### Testing
- **Unit Tests** — 541 tests covering all expression builders, mappers, converters, and configuration
- **Integration Tests** — 34 end-to-end tests using Testcontainers.DynamoDb against DynamoDB Local
- **Test Coverage** — 100% specification coverage across 15 specs

### Dependencies
- `AWSSDK.DynamoDBv2` (>= 3.7.x)
- `Microsoft.Extensions.Logging.Abstractions` (>= 8.0.0)
- `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 8.0.0)
- `Microsoft.Extensions.Options` (>= 8.0.0)
- `MinVer` (>= 6.0.0) — build-time only

[0.1.2]: https://github.com/lesandiz/dynamodb-expression-mapping/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/lesandiz/dynamodb-expression-mapping/releases/tag/v0.1.1
