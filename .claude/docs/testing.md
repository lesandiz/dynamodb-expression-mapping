# Testing Guide

## Test Framework & Conventions

- **xUnit**, **FluentAssertions**, **NSubstitute**, **Bogus**, **Testcontainers.DynamoDb**
- Unit/property test project: `tests/DynamoDb.ExpressionMapping.Tests/` — no Docker dependency
- Integration test project: `tests/DynamoDb.ExpressionMapping.IntegrationTests/` — uses `[Collection("DynamoDb")]` with a shared `DynamoDbFixture` (`IAsyncLifetime`) via Testcontainers; each test class creates/deletes its own table
- Integration tests are in a separate project to prevent xUnit's eager collection fixture initialization from triggering Docker container startup during unit-only test runs (including Stryker mutation testing and coverage collection)
- The integration test project references the unit test project for shared fixtures; the main library flows in as a transitive dependency (no explicit `ProjectReference` to `DynamoDb.ExpressionMapping.csproj` needed)
- Spec 12 (`.ralph/core/specs/12-testing-strategy.md`) contains the complete test plan with every test case listed

## Commands

```bash
dotnet test tests/DynamoDb.ExpressionMapping.Tests/              # unit + property tests (no Docker)
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/   # integration tests (requires Docker)
dotnet test --filter "Category=Property"                          # property tests only
dotnet test --filter "Category!=Property"                         # unit tests only
```

## Property-Based Tests

- Default 100 iterations per property (fast local feedback). CI sets `FSCHECK_MAX_TEST=10000`.
- All property test classes tagged with `[Trait("Category", "Property")]`.
- On Windows, `FSCHECK_MAX_TEST=100 dotnet test` does **not** propagate the env var to the .NET test host. The default of 100 in `PropertyTestConfig.cs` handles the local case.

## Verification Strategy

Tiered testing to maintain a fast feedback loop while ensuring affected code is always verified before committing.

### Pre-commit (must pass before every commit)

Run affected projects, **excluding property tests** (they are slow even at low iteration counts due to per-iteration expression tree generation cost). Target: **under 1 minute**.

```bash
dotnet test tests/DynamoDb.ExpressionMapping.Tests/ --filter "Category!=Property"
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/   # when integration code is affected
```

**Rule:** If code was moved, refactored, or its project references changed, the affected tests must be **executed** — a successful build is not verification. Do not defer to CI.

### Pre-complete (must pass before marking a PLAN.md item done)

Full test suite including property tests.

```bash
dotnet test tests/DynamoDb.ExpressionMapping.Tests/              # all unit + property tests
dotnet test tests/DynamoDb.ExpressionMapping.IntegrationTests/   # integration tests
```

### CI-only (not required locally)

```bash
dotnet test --filter "Category=Property"  # CI sets FSCHECK_MAX_TEST=10000
dotnet stryker
```

## Soak Tests

Soak and concurrency testing harness. DynamoDB Local runs on **port 8004** (host) mapping to 8000 (container).

```bash
cd tests/DynamoDb.ExpressionMapping.SoakTests && docker compose up -d
# Run soak tests: dotnet run -- --duration 10 --concurrency 8
```
