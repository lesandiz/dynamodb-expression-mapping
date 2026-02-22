# Test Suite Refactoring Plan

## Context

Phase 3b mutation testing added 400 tests that boosted mutation score to 90.8%, but many are repetitive `[Fact]` tests that differ only by input data. The test suite also has scattered test entities and two near-identical composability test files. This refactoring consolidates redundancy, centralizes shared entities, and distributes mutation-killing tests to their proper subsystem test files — all without losing any test coverage or mutation detection capability.

**Invariant:** All tests must pass after each step. Mutation score must remain >= 90.8%.

---

## Step 1: Centralize Scattered Test Entities

**New file:** `Fixtures/ExpressionTestEntities.cs`

Move inline entity definitions to shared fixtures:
- `FilterTestEntity`, `FilterAddress`, `OrderStatus` from `Expressions/FilterExpressionBuilderTests.cs:570-616`
- `UpdateTestEntity`, `TestPriority` from `Expressions/UpdateExpressionBuilderTests.cs:348-376`
- `KeyConditionTestEntity`, `RemappedEntity`, `NestedAddress` from `Expressions/KeyConditionExpressionBuilderTests.cs:405-436`

Remove the inline definitions from source files. Add `using DynamoDb.ExpressionMapping.Tests.Fixtures;` where needed.

**Keep in-place** (only used by their own file): `MutR2EntityWithEnum`, `MutR2Status`, `MutR2EntityWithField`, `MutR2EntityWithList`, `ValueHolder`, `MutTestLenientEntity`.

**Files modified:** `FilterExpressionBuilderTests.cs`, `UpdateExpressionBuilderTests.cs`, `KeyConditionExpressionBuilderTests.cs`, `ConditionExpressionBuilderTests.cs` (already imports FilterTestEntity from filter file)
**Files created:** `Fixtures/ExpressionTestEntities.cs`

---

## Step 2: Composability Test Consolidation (41 methods → 21 + 1)

**Approach:** Abstract base class with two thin derived classes.

**New file:** `Expressions/ExpressionResultComposabilityTestBase.cs`
- Abstract base with 20 concrete `[Fact]` test methods
- Abstract members: `CreateResult(expr, names, values)`, `CreateEmptyResult()`, `ComposeAnd(left, right)`, `ComposeOr(left, right)`, plus `GetExpression()`, `GetNames()`, `GetValues()`, `IsEmpty()` accessors
- Properties: `NamePrefix` (`"#filt_"` / `"#cond_"`), `ValuePrefix` (`":filt_v"` / `":cond_v"`)

**Modified files:**
- `Expressions/FilterExpressionResultComposabilityTests.cs` — replace 622-line file with ~40-line derived class implementing the abstract factory methods
- `Expressions/ConditionExpressionResultComposabilityTests.cs` — replace 653-line file with ~50-line derived class + the one unique `[Fact]` test `ComposedConditions_UseCondScopeNotFiltScope`

xUnit discovers tests from both derived classes, so total test executions remain 41.

---

## Step 3: Split and Consolidate P3MutationKillingTests (109 methods → ~45)

**File:** `ResultMapping/P3MutationKillingTests.cs` (1452 lines) → split into 3 files, delete original.

### 3a: `ResultMapping/AttributeValueReaderTests.cs` (new)
Consolidate the ~58 `ReadXxx` tests using `[Theory]`:

| Theory Method                                    | Replaces                                        | Data Rows                      |
| ------------------------------------------------ | ----------------------------------------------- | ------------------------------ |
| `NumericReader_ValidValue_ReturnsParsed`         | ReadInt/Long/Decimal valid tests                | 3 rows (int, long, decimal)    |
| `FloatingPointReader_ValidValue_ReturnsParsed`   | ReadDouble/Float valid tests                    | 2 rows (use `BeApproximately`) |
| `NumericReader_MissingKey_ReturnsDefault`        | ReadInt/Long/Decimal/Double/Float missing tests | 5 rows                         |
| `NumericReader_NullAttribute_ReturnsDefault`     | ReadInt/Long/Decimal/Double/Float null tests    | 5 rows                         |
| `NumericReader_InvalidFormat_ReturnsDefault`     | ReadInt/Long/Decimal/Double/Float invalid tests | 5 rows                         |
| `NullableNumericReader_Missing_ReturnsNull`      | ReadNullableInt/Long/etc missing tests          | 5 rows                         |
| `NullableNumericReader_ValidValue_ReturnsParsed` | ReadNullableInt/Long/etc valid tests            | 5 rows                         |

Keep as individual `[Fact]`: ReadString (4 tests), ReadBool (3 tests), ReadBytes (4 tests), ReadStringList (4 tests), NavigateToLeaf (6 tests) — these have distinct setup/assertion patterns.

### 3b: `ResultMapping/MappingStrategyTests.cs` (new)
Move verbatim: CompositeMappingStrategy (4 tests), SinglePropertyMappingStrategy (6 tests), IdentityMappingStrategy (2 tests). These are distinct behavioral tests, no consolidation needed.

### 3c: Append to `ResultMapping/DirectResultMapperTests.cs`
Move: DirectResultMapper null-guard and constructor tests (7 tests). They belong with the mapper's primary test file.

**Delete:** `ResultMapping/P3MutationKillingTests.cs`

---

## Step 4: Distribute and Consolidate P4MutationKillingTests (54 methods → ~30)

**File:** `P4MutationKillingTests.cs` (1094 lines) → distribute to subsystem files, delete original.

### 4a: `Caching/CacheStatisticsTests.cs` (new)
Consolidate 20 hit-rate `[Fact]` tests into 3 `[Theory]` methods + 2 `[Fact]` for OverallHitRate edge cases:

```
[Theory]
[InlineData(0, 0, 0.0)]
[InlineData(10, 0, 1.0)]
[InlineData(0, 10, 0.0)]
[InlineData(3, 1, 0.75)]
[InlineData(1, 1, 0.5)]
public void ProjectionHitRate_ReturnsCorrectRate(int hits, int misses, double expected)
```

Same pattern for `MapperHitRate` and `FilterHitRate`.

### 4b: Append to `Caching/ExpressionCacheTests.cs`
Move: ExpressionCache TrackAccess tests (5 tests), verbatim.

### 4c: `Extensions/InternalRequestExtensionsTests.cs` (new)
Move: The 12 `??=` preservation tests + their reflection helpers. Keep as `[Fact]` — request type differences make parameterization more complex than beneficial.

### 4d: Append to existing files
- DynamoDbExpressionConfig.Builder null-coalescing tests (8 tests) → `DynamoDbExpressionConfigTests.cs`
- AliasGenerator.Clone tests (4 tests) → `ReservedKeywords/AliasGeneratorTests.cs`
- RequestMergeHelpers empty-source tests (3 tests) → `Extensions/RequestMergeHelpersTests.cs`
- WithUpdate extension test (1 test) → `Extensions/UpdateExtensionsTests.cs`

**Delete:** `P4MutationKillingTests.cs`

---

## Step 5: Consolidate P2MutationKillingTests Regions A & B (18 methods → 2)

**File:** `Mapping/P2MutationKillingTests.cs`

### 5a: Region A — Converter FromNull (11 → 1)
Replace 11 individual `[Fact]` tests with one `[Theory]` using `[MemberData]`:

```csharp
public static IEnumerable<object[]> ConverterFromNullData => ...
// Rows: (BoolConverter, false), (Int32Converter, 0), (Int64Converter, 0L),
//        (DoubleConverter, 0.0), (DecimalConverter, 0m), (GuidConverter, Guid.Empty),
//        (DateTimeConverter, DateTime.MinValue), (DateTimeOffsetConverter, DateTimeOffset.MinValue),
//        (StringConverter, null), (ByteArrayConverter, null)

[Theory]
[MemberData(nameof(ConverterFromNullData))]
public void Converter_FromNull_ReturnsDefault(dynamic converter, object? expected)
```

### 5b: Region B — Empty/Missing Values (7 → 1)
Same approach: one `[Theory]` with `[MemberData]` supplying converter + empty AttributeValue + expected default.

### 5c: Rename file
Rename `P2MutationKillingTests.cs` → `ConverterEdgeCaseTests.cs` (same directory). Update namespace accordingly. All remaining regions (C through N) stay as-is — they're distinct behavioral tests.

---

## Verification

After each step:
```bash
dotnet test tests/DynamoDb.ExpressionMapping.Tests/ --filter "Category!=Property"
```

After all steps complete:
```bash
dotnet test tests/DynamoDb.ExpressionMapping.Tests/ --verbosity normal
```

Verify: total test count remains >= 1260 (xUnit counts each `[Theory]` data row as a separate test). No failures.

---

## Summary

| Step      | Action                             | Methods Before   | Methods After   | Files Created | Files Deleted |
| --------- | ---------------------------------- | ---------------- | --------------- | ------------- | ------------- |
| 1         | Centralize entities                | —                | —               | 1             | 0             |
| 2         | Composability base class           | 41               | 22              | 1             | 0             |
| 3         | Split P3, consolidate readers      | 109              | ~45             | 2             | 1             |
| 4         | Distribute P4, consolidate stats   | 54               | ~30             | 2             | 1             |
| 5         | Consolidate P2 regions A/B, rename | 18               | 2               | 0 (rename)    | 0 (rename)    |
| **Total** |                                    | **~222 methods** | **~99 methods** | **6 new**     | **2 deleted** |

Test execution count unchanged (Theory rows counted individually). ~123 fewer test methods to maintain.
