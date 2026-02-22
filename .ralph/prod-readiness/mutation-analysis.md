# Mutation Analysis Report — Phase 3b.4

> Generated: 2026-02-22 | Stryker 4.12.0 | 607 unit tests (property tests excluded)
> Report: `StrykerOutput/2026-02-22.16-16-19/reports/mutation-report.html`

---

## Overall

- **2352** mutants created | **801** tested | **634** killed | **167** survived | **152** NoCoverage
- **Mutation Score: 66.5%** (target: 80% overall, 90% expression builders)
- **319** compile errors (Stryker safe mode) | **753** ignored (string mutations) | **4** ignored (mutate filter)

---

## Per-Subsystem Breakdown

| Subsystem        | Killed | Survived | NoCov | **Score** | Target  |
| ---------------- | ------ | -------- | ----- | --------- | ------- |
| ReservedKeywords | 15     | 0        | 0     | **100%**  | —       |
| Exceptions       | 10     | 0        | 0     | **100%**  | —       |
| Root             | 29     | 5        | 0     | **85.3%** | 80%     |
| Mapping          | 174    | 44       | 17    | **74.0%** | 80%     |
| Extensions       | 53     | 21       | 2     | **69.7%** | 80%     |
| Expressions      | 271    | 60       | 65    | **68.4%** | **90%** |
| Caching          | 31     | 21       | 4     | **55.4%** | 80%     |
| ResultMapping    | 51     | 16       | 64    | **38.9%** | 80%     |

---

## Priority 1 — Expression Builders (target ≥ 90%)

### UpdateExpressionBuilder.cs — 38 survivors
- **Pattern**: Statement removal on fluent method null-guard clauses (`ArgumentNullException.ThrowIfNull` calls)
- **Fix**: Add null-argument tests for every fluent method (Set, Remove, Add, Delete, etc.)
- **Also**: Build() deduplication/overlap logic untested

### FilterExpressionVisitor.cs — 34 survivors (mix of Survived + NoCoverage)
- **Pattern**: NoCoverage on uncommon expression paths (Convert, ConvertChecked, error formatting)
- **Pattern**: `||` → `&&` logical mutations in method dispatch survive
- **Pattern**: Null coalescing on 6 constructor args
- **Fix**: Add tests for uncommon expression tree node types, null constructor args

### SortKeyConditionBuilder.cs — 16 survivors
- **Pattern**: Validation guard statements, boundary condition (`>=` vs `>` in Between validation)
- **Fix**: Add boundary-value tests for Between, test guard clause exceptions

### ConditionExpressionResult.cs / FilterExpressionResult.cs — re-aliasing logic
- **Pattern**: `OrderByDescending` → `OrderBy` mutations survive in re-aliasing
- **Pattern**: Equality mutation `idx >= max` in collision detection
- **Pattern**: NoCoverage on collision detection paths
- **Fix**: Test composition with alias collisions, verify deterministic alias ordering

### ProjectionBuilder.cs / ProjectionResult.cs — null coalescing
- **Pattern**: Null coalescing (remove right) on constructor args
- **Fix**: Add null-argument tests

---

## Priority 2 — Type Conversion (target ≥ 80%)

### Converter FromAttributeValue null-handling — ~15 survivors across 10 files
- **Affected**: BoolConverter, DateTimeConverter, DateTimeOffsetConverter, DoubleConverter, GuidConverter, Int32Converter, Int64Converter, ArrayConverter, ListConverter, MapConverter, NullableConverter
- **Pattern**: `||` → `&&` in null/NULL checks — tests don't cover null AttributeValue inputs
- **Pattern**: Null coalescing (remove right) on constructor params for collection converters
- **Fix**: Add `FromAttributeValue(null)` and `FromAttributeValue(new AttributeValue { NULL = true })` tests

### SetConverter.cs — 13 survivors
- **Pattern**: Boundary conditions in SS/NS/L detection
- **Fix**: Add tests for each DynamoDB set type (StringSet, NumberSet, List fallback)

### AttributeNameResolver.cs — 8 survivors
- **Pattern**: Statement mutations on property override/ignore registration
- **Fix**: Test that override/ignore calls actually modify resolution behavior

### AttributeValueConverterRegistry.cs — mixed
- **Pattern**: Registration and Clone logic
- **Fix**: Test Clone-then-register pattern, verify isolation

---

## Priority 3 — Result Mapping (target ≥ 80%)

### AttributeValueReader.cs — 60 NoCoverage mutants
- **Entire class is untested** — biggest single-file coverage gap
- All type-specific Read methods (ReadString, ReadNumber, ReadBool, etc.) have zero coverage
- **Fix**: Write unit tests for each Read method with valid, null, and type-mismatch inputs

### CompositeMappingStrategy.cs — off-by-one survivors
- **Pattern**: Equality/arithmetic mutation on path traversal (`i < Length - 1`)
- **Fix**: Test nested property mapping with varying path depths

### SinglePropertyMappingStrategy.cs — logical mutation
- **Pattern**: `&&` → `||` in condition check
- **Fix**: Test with edge-case inputs that exercise both branches

### DirectResultMapper.cs — null coalescing on constructor args
- **Fix**: Add null-argument tests

---

## Priority 4 — Supporting Systems (target ≥ 80%)

### CacheStatistics.cs — 20 survivors
- **Pattern**: Arithmetic/conditional mutations in hit-rate computation properties
- All 4 hit-rate properties (ProjectionHitRate, MapperHitRate, FilterHitRate, OverallHitRate) untested
- **Fix**: Create CacheStatistics with known hit/miss counts, assert computed rates

### ExpressionCache.cs — 2 survivors
- Statement removal and negate expression (`!(isHit)`)
- **Fix**: Verify cache hit/miss side effects

### InternalRequestExtensions.cs — 10 survivors
- **Pattern**: `??=` → `=` mutations — tests never pass pre-populated ExpressionAttributeNames/Values
- **Fix**: Test applying expressions to requests that already have attribute dictionaries

### DynamoDbExpressionConfig.cs — 5 survivors
- **Pattern**: Null coalescing and coalesce-assignment
- **Fix**: Test builder with explicit null overrides and default fallbacks

---

## Common Mutation Patterns (cross-cutting)

| Pattern                                       | Count | Fix Strategy                                 |
| --------------------------------------------- | ----- | -------------------------------------------- |
| Null coalescing (remove right/left)           | ~30   | Add null-argument tests to constructors      |
| Statement removal on guard clauses            | ~25   | Add tests passing null to guarded params     |
| `\|\|` → `&&` in null checks                  | ~15   | Test with null/NULL AttributeValue inputs    |
| `??=` → `=` (coalesce-assign)                 | ~10   | Test with pre-populated dictionaries         |
| Block removal `{}`                            | ~10   | Verify side effects of removed blocks        |
| Arithmetic boundary (`>` vs `>=`, off-by-one) | ~8    | Boundary-value tests                         |
| OrderByDescending → OrderBy                   | ~4    | Verify deterministic ordering in composition |

---

## Next Steps (Phase 3b.5–3b.10)

1. **3b.5**: Triage expression builder survivors — classify as equivalent vs killable
2. **3b.6**: Write tests to kill non-equivalent mutants in expression builders → target 90%
3. **3b.7**: Triage and fix type conversion survivors → target 80%
4. **3b.8**: Triage and fix result mapping survivors → target 80%
5. **3b.9**: Triage and fix supporting systems survivors → target 80%
6. **3b.10**: Re-run full mutation analysis, verify thresholds met
