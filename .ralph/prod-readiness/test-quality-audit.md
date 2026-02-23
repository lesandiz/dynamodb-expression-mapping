# Test Quality Audit Report

**Date:** 2026-02-23
**Branch:** `ralph/prod-readiness`
**Commits:** `d71e978`, `a0e6d2e` (Regex/Random.Shared/Gen.Where fixes)

## Summary

Full audit of all test projects (~55 test files) identified 45 test quality issues across 3 categories. All fixes applied, build passes, 974 unit tests green.

### Changes by Category

| Category                                  | Count  | Action                            |
| ----------------------------------------- | ------ | --------------------------------- |
| Misleading names / missing key assertions | 12     | Fixed assertions, renamed methods |
| Trivially true / weak assertions          | 10     | Removed or strengthened           |
| Redundant duplicate tests                 | 23     | Deleted                           |
| **Total**                                 | **45** | **-1,016 lines across 20 files**  |

### Performance Fixes (property-based tests)

| Issue                                        | Files                            | Fix                                                                        |
| -------------------------------------------- | -------------------------------- | -------------------------------------------------------------------------- |
| `Random.Shared.Next()` in FsCheck generators | `FilterPredicateGenerator.cs`    | Replaced 5 calls with `Gen.Choose`/`Gen.Elements`                          |
| `new Regex(...)` per iteration               | 4 property test files            | Cached 13 patterns as `static readonly Regex` with `RegexOptions.Compiled` |
| `Gen.Where` crashes test host                | `ProjectionSelectorGenerator.cs` | Replaced with pre-computed unique combinations (see below)                 |

### Critical Fix: `Gen.Where` Test Host Crash

**Root cause**: `Gen.Where` in FsCheck 3.0.0-rc3 crashes the test host process (StackOverflow in retry/shrink mechanism) when used inside nested `Gen.SelectMany` chains. `ProjectionSelectorGenerator` used `Gen.Where` to enforce uniqueness constraints on 2/3/4-element property selections, combined with 2-4 levels of `Gen.SelectMany` nesting.

**Symptoms**:
- `dotnet test` process hangs indefinitely after tests complete (appears as "slow tests")
- Running `ProjectionBuilderProperties` Composite or Complex tier tests crashes the test host
- VSTest reports "Test host process crashed" after ~7 minutes
- All other property test classes (which don't use `Gen.Where`) exit cleanly

**Fix**: Replaced all `Gen.Where` calls with pre-computed unique combinations:
- `CompositeProjectionGen()`: Pre-computed unique pairs/triples of `PropertyInfo`
- `ComplexProjectionGen()`: Pre-computed unique path pairs/triples/quads with guaranteed nested property inclusion
- Also fixed flaky `ComplexProjections_ShouldGenerateValidNestedPropertySelectors` — Complex tier now guarantees at least one nested property path

**Result**: Full property suite (102 tests) runs in 277ms with clean exit. Full test suite (1,076 tests) completes in ~1 second.

## Files Modified

### Unit Tests (`DynamoDb.ExpressionMapping.Tests`)

- `Mapping/ConverterEdgeCaseTests.cs` -- fixed broken override test, deleted 3 dups
- `Mapping/ConverterRegistryTests.cs` -- folded dup, deleted misleading test
- `Mapping/AttributeNameMappingTests.cs` -- deleted trivially true caching test
- `Expressions/FilterExpressionBuilderTests.cs` -- fixed enum test, deleted trivially true test, absorbed 6 Round2 tests + `MutR2EntityWithField`
- `Expressions/ProjectionBuilderTests.cs` -- deleted dup
- `Expressions/PropertyPathTests.cs` -- removed trivially true assertion
- `Expressions/MutationKillingTests.cs` -- strengthened 3 weak assertions
- `Expressions/MutationKillingRound2Tests.cs` -- deleted 15 redundant tests, then **deleted file** (6 survivors moved to `FilterExpressionBuilderTests.cs`)
- `Expressions/MutationKillingRound3Tests.cs` -- renamed 3 tests, fixed 1 assertion, deleted 1 dup
- `Expressions/ExpressionResultComposabilityTestBase.cs` -- deleted 4 redundant tests
- `Expressions/ConditionExpressionResultComposabilityTests.cs` -- deleted trivially true test
- `Extensions/FilterExtensionsTests.cs` -- deleted cross-file dup
- `Extensions/UpdateExtensionsTests.cs` -- deleted same-file dup
- `Extensions/RequestMergeHelpersTests.cs` -- added inner exception assertions
- `Caching/ExpressionCacheTests.cs` -- fixed thread-safety test, fixed category test, deleted 4 dups
- `Caching/NullExpressionCacheTests.cs` -- deleted 2 redundant tests
- `Caching/CacheStatisticsTests.cs` -- deleted 2 redundant tests
- `PropertyBased/Generators/FilterPredicateGenerator.cs` -- replaced `Random.Shared` with `Gen` combinators
- `PropertyBased/Generators/FilterPredicateGeneratorTests.cs` -- removed trivially true assertions
- `PropertyBased/FilterExpressionBuilderProperties.cs` -- cached Regex patterns
- `PropertyBased/UpdateExpressionBuilderProperties.cs` -- cached Regex patterns
- `PropertyBased/KeyConditionBuilderProperties.cs` -- cached Regex patterns, replaced 2 remaining dynamic Regex with static `PartitionKeyEqualityRegex`/`SortKeyReferenceRegex`
- `PropertyBased/ProjectionBuilderProperties.cs` -- cached Regex patterns
- `PropertyBased/Generators/ProjectionSelectorGenerator.cs` -- eliminated `Gen.Where` (test host crash fix)
- `Fixtures/ExpressionTestEntities.cs` -- added `MutR2EntityWithList` (moved from Round2 file)
- `xunit.runner.json` -- **new** (test runner configuration)
- `DynamoDb.ExpressionMapping.Tests.csproj` -- added `<Content>` item for `xunit.runner.json`

### Integration Tests (`DynamoDb.ExpressionMapping.IntegrationTests`)

- `Integration/CombinedExpressionIntegrationTests.cs` -- deleted dup, fixed OR assertions, removed trivially true HTTP 200
- `Integration/ConditionIntegrationTests.cs` -- added condition-applied assertion
- `xunit.runner.json` -- **new** (test runner configuration)
- `DynamoDb.ExpressionMapping.IntegrationTests.csproj` -- added `<Content>` item for `xunit.runner.json`

## Issues NOT Addressed (intentional)

These were identified but intentionally left as-is:

| Issue                                                                                | Reason                                                                                                          |
| ------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| `ConditionExpressionBuilderTests` ~20 tests duplicate `FilterExpressionBuilderTests` | Defense-in-depth for a separate public API class; file header acknowledges this                                 |
| `InternalRequestExtensionsTests` duplicates per-extension tests                      | Tests internal API layer separately; valid testing strategy                                                     |
| `DynamoDbExpressionConfigTests` individual property tests                            | Useful for debugging isolated failures                                                                          |
| Property test slowness (102 tests x 100 iterations)                                  | Tests complete in 277ms; prior "slowness" was caused by `Gen.Where` crash (now fixed)                           |

## Recommended Next Steps

### ~~1. Commit pending fixes~~ ✓ Done
Committed as `a0e6d2e`.

### ~~2. Add `xunit.runner.json` for test output configuration~~ ✓ Done
Added to both test projects with `<Content>` items in `.csproj` files.

### ~~3. Consolidate remaining MutationKilling round files~~ ✓ Done
`MutationKillingRound2Tests.cs` deleted. 6 surviving tests moved to `FilterExpressionBuilderTests.cs`. `MutR2EntityWithField` moved alongside tests; `MutR2EntityWithList` moved to shared `ExpressionTestEntities.cs`; unused entities (`MutR2EntityWithEnum`, `MutR2Status`, `ValueHolder`) deleted.

### ~~4. Address dynamic Regex in KeyConditionBuilderProperties~~ ✓ Done
Replaced 2 per-iteration `new Regex(...)` calls with static readonly `PartitionKeyEqualityRegex` and `SortKeyReferenceRegex` fields. All callers pass constant values ("PK"/"SK"), so the cached patterns are equivalent.

### ~~5. Implement `KeyConditionOperationGenerator`~~ ✓ Done
Replaced `ExpressionGenerators.KeyConditionPredicate` (which threw `NotImplementedException`) with `ExpressionGenerators.KeyConditionOperation` backed by `KeyConditionOperationGenerator`. Generator produces `Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>` across Simple (PK only), Composite (PK + SK comparison), and Complex (PK + BETWEEN/begins_with) tiers. Added 7 generator smoke tests and 3 generator-based property tests to `KeyConditionBuilderProperties`.
