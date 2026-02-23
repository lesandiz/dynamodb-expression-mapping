# CLAUDE.md

## Project Overview

`DynamoDb.ExpressionMapping` — a .NET 8.0+ library that converts C# LINQ expression trees into DynamoDB expression strings (`ProjectionExpression`, `FilterExpression`, `ConditionExpression`, `UpdateExpression`, `KeyConditionExpression`) with direct result mapping that avoids full entity hydration.

**Spec-first project.** All specs live in `.ralph/core/specs/` and define the complete architecture, API surface, and test plan.

## Architecture

Six subsystems — see individual specs for detailed API surface and behavior:

- **Expression Builders** (Specs 02, 03, 06, 07, 13) — `ProjectionExpressionVisitor`, `ProjectionBuilder`, `FilterExpressionBuilder`, `ConditionExpressionBuilder`, `UpdateExpressionBuilder`, `KeyConditionExpressionBuilder`
- **Attribute Name Resolution** (Spec 01) — `IAttributeNameResolver<T>`, `IAttributeNameResolverFactory`. Resolution: fluent overrides → `[DynamoDbAttribute]` → `[DynamoDBProperty]` → property name
- **Type Converters** (Spec 05) — `IAttributeValueConverter<T>`, `AttributeValueConverterRegistry`, `ExpressionValueEmitter`. Resolution: `[DynamoDbConverter]` → registry exact → Nullable → Enum → open-generic collection → throw
- **Direct Result Mapping** (Spec 04) — `IDirectResultMapper<TSource>`, compiles mapping delegates via expression trees
- **Reserved Keywords & Alias Scoping** (Spec 08) — scoped prefixes (`#proj_`, `#filt_`, `#cond_`, `#upd_`, `#key_`) prevent collisions; `FilterExpressionResult.And()`/`Or()` re-alias for safe composition
- **AWS SDK Integration** (Spec 10) — extension methods on SDK request types, `RequestMergeHelpers` for dictionary merging

## Key Design Decisions

- **Generic-first**: All public APIs generic over `TSource`, not coupled to specific models
- **`PropertyPath.SegmentProperties`**: Carries `PropertyInfo` per segment — no extra reflection during resolution
- **Factory pattern**: `IAttributeNameResolverFactory` auto-discovers types; only deviations need config
- **Fail-fast**: Validation errors throw at build time, never during query execution
- **Exception hierarchy**: `ExpressionMappingException` → `UnsupportedExpressionException`, `MissingConverterException`, `ExpressionAttributeConflictException`, `InvalidExpressionException` → `InvalidProjection/Filter/Update/KeyCondition`

## Build & Test

Docker Desktop is available in the dev environment. Always run integration and soak tests locally — do not defer them to CI.

```bash
dotnet build
dotnet test tests/DynamoDb.ExpressionMapping.Tests/ --filter "Category!=Property"   # pre-commit (fast)
dotnet test tests/DynamoDb.ExpressionMapping.Tests/                                  # all unit + property tests
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/                       # integration (Testcontainers, auto-manages Docker)
```

Soak & concurrency tests (DynamoDB Local on port 8004):
```bash
docker compose -f tests/DynamoDb.ExpressionMapping.SoakTests/docker-compose.yml up -d
dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests/ -- --concurrency-scenarios   # concurrency scenarios
dotnet run --project tests/DynamoDb.ExpressionMapping.SoakTests/ -- --duration 2              # smoke soak test
docker compose -f tests/DynamoDb.ExpressionMapping.SoakTests/docker-compose.yml down
```

For detailed testing guide, verification strategy, and framework conventions see `.claude/docs/testing.md`.

## Namespace & Dependencies

Root: `DynamoDb.ExpressionMapping` — Sub: `Attributes`, `Mapping`, `Expressions`, `ResultMapping`, `ReservedKeywords`, `Extensions`, `Exceptions`, `Caching`

Dependencies: `AWSSDK.DynamoDBv2` (>= 3.7.x), `Microsoft.Extensions.Logging.Abstractions` (>= 8.0.0, optional), `Microsoft.Extensions.DependencyInjection.Abstractions` (>= 8.0.0, optional)

## Further Reading

- Testing guide & verification strategy: `.claude/docs/testing.md`
- Example projects (ConsoleQuickStart, WebApiExample, SoakTests): `.claude/docs/examples.md`
- Full test plan: `.ralph/core/specs/12-testing-strategy.md`
