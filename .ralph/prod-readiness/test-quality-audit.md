# Test Quality Audit Report

**Date:** 2026-02-23
**Branch:** `ralph/prod-readiness`
**Commits:** `d71e978`, pending (Regex/Random.Shared fixes)

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
- `Expressions/FilterExpressionBuilderTests.cs` -- fixed enum test, deleted trivially true test
- `Expressions/ProjectionBuilderTests.cs` -- deleted dup
- `Expressions/PropertyPathTests.cs` -- removed trivially true assertion
- `Expressions/MutationKillingTests.cs` -- strengthened 3 weak assertions
- `Expressions/MutationKillingRound2Tests.cs` -- deleted 15 redundant tests
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
- `PropertyBased/KeyConditionBuilderProperties.cs` -- cached Regex patterns
- `PropertyBased/ProjectionBuilderProperties.cs` -- cached Regex patterns
- `PropertyBased/Generators/ProjectionSelectorGenerator.cs` -- eliminated `Gen.Where` (test host crash fix)

### Integration Tests (`DynamoDb.ExpressionMapping.IntegrationTests`)

- `Integration/CombinedExpressionIntegrationTests.cs` -- deleted dup, fixed OR assertions, removed trivially true HTTP 200
- `Integration/ConditionIntegrationTests.cs` -- added condition-applied assertion

## Issues NOT Addressed (intentional)

These were identified but intentionally left as-is:

| Issue                                                                                | Reason                                                                                                          |
| ------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------- |
| `ConditionExpressionBuilderTests` ~20 tests duplicate `FilterExpressionBuilderTests` | Defense-in-depth for a separate public API class; file header acknowledges this                                 |
| `InternalRequestExtensionsTests` duplicates per-extension tests                      | Tests internal API layer separately; valid testing strategy                                                     |
| `DynamoDbExpressionConfigTests` individual property tests                            | Useful for debugging isolated failures                                                                          |
| Property test slowness (102 tests x 100 iterations)                                  | Tests complete in 277ms; prior "slowness" was caused by `Gen.Where` crash (now fixed)                           |

## Recommended Next Steps

### 1. Commit pending fixes
The Regex caching, Random.Shared, and Gen.Where fixes are complete but not yet committed.

### 2. Add `xunit.runner.json` for test output configuration
No runner config exists. Adding one would improve developer experience:
```json
{
  "diagnosticMessages": false,
  "parallelizeTestCollections": true,
  "methodDisplay": "method"
}
```

### 3. Consolidate remaining MutationKilling round files
`MutationKillingRound2Tests.cs` lost 15 of its tests in this audit. Review remaining tests and consider folding survivors into subsystem-specific test files (continuing the pattern from Phase 3c).

### 4. Address dynamic Regex in KeyConditionBuilderProperties
`ValidatePartitionKeyEquality` and `ValidateSortKeyCondition` still create `new Regex(...)` per iteration with interpolated values. These could be refactored to use string operations or pre-built pattern templates if performance matters.

### 5. Review `ExpressionGenerators.KeyConditionPredicate`
Still throws `NotImplementedException("KeyConditionPredicateGenerator - deferred")`. The `KeyConditionBuilderProperties` tests work around this by using direct builder calls, but completing the generator would enable property-based testing parity with filter/projection/update.
