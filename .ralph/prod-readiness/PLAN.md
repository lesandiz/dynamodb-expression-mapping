# Production Readiness Plan

> Source of truth: `specs/PR-00` through `PR-07`. This file is a task list only — implementation details live in the specs.

---

## Phase 1 — Property-Based Testing (PR-01) ✅ COMPLETE

All property-based tests implemented and passing at 10k iterations. Critical bug discovered and fixed: UpdateExpressionBuilder orphaned placeholders on duplicate property updates. See `archive/completed_phases.md` for full details.

---

## Phase 2 — Soak & Concurrency Testing (PR-02) ✅ COMPLETE

**30-minute soak test PASSED with zero operation failures.**

**Results:**
- Operations: 3,171,761 (0 failed), 1599.2 ops/sec ✅
- Gen2 collections: 0 ✅
- Cache: 8 entries (stable), 100% hit ratio ✅
- Memory: Fixed baseline calculation (was using process start instead of post-warm-up)

**See `archive/phase2_detailed_progress.md` for full timeline of bug discoveries and resolutions.**

---

## Phase 3a — Integration Test Project Isolation ✅ COMPLETE

Moved all integration tests and the `DynamoDbFixture` into a dedicated `DynamoDb.ExpressionMapping.IntegrationTests` project so xUnit never discovers the `DynamoDbCollection` fixture when running the unit test project.

- [x] 3a.1 Created `tests/DynamoDb.ExpressionMapping.IntegrationTests/DynamoDb.ExpressionMapping.IntegrationTests.csproj` with Testcontainers dependency
- [x] 3a.2 Moved `Integration/` folder (8 files: 7 test classes + `DynamoDbFixture.cs`) to new project, updated namespaces
- [x] 3a.3 Removed `Testcontainers.DynamoDb` package reference from `DynamoDb.ExpressionMapping.Tests.csproj`
- [x] 3a.4 Added new project to solution file (`DynamoDb.ExpressionMapping.slnx`)
- [x] 3a.5 Stryker config already only referenced unit test project — no change needed
- [x] 3a.6 Verified unit tests run without Docker (all tests pass including property-based tests, no Docker startup)
- [x] 3a.7 Integration test project builds successfully; runtime verification deferred to CI (requires Docker)
- [x] 3a.8 Updated CI workflows: `ci.yml` targets unit test project directly, `integration-tests.yml` targets new integration project
- [x] 3a.9 Added `InternalsVisibleTo("DynamoDb.ExpressionMapping.IntegrationTests")` to `AssemblyInfo.cs` for internal API access

**Additional change:** Integration test project references the unit test project to access shared fixtures (`TestEntity`, `TestIntegrationEntity`, etc.) in `DynamoDb.ExpressionMapping.Tests.Fixtures` namespace. The main library (`DynamoDb.ExpressionMapping`) flows in as a transitive dependency — no explicit project reference needed.

---

## Phase 3b — Mutation Testing (PR-03) ✅ COMPLETE

Mutation score improved from 66.5% to **90.8% overall** (865 killed, 70 survived, 18 NoCoverage out of 953 testable mutants). All subsystem targets met. 307 mutation-killing tests added across 6 test files.

**Final scores (2026-02-22):**

| Subsystem | Initial | Final | Target | Status |
|---|---|---|---|---|
| Expressions | 68.4% | 91.4% | **90%** | ✅ |
| Mapping | 74.0% | 91.9% | 80% | ✅ |
| Extensions | 69.7% | 82.9% | 80% | ✅ |
| Caching | 55.4% | 94.6% | 80% | ✅ |
| ResultMapping | 38.9% | 88.5% | 80% | ✅ |
| Root | 85.3% | 88.2% | 80% | ✅ |
| Exceptions | 100% | 100% | — | ✅ |
| ReservedKeywords | 100% | 100% | — | ✅ |

**Remaining survivors (70S + 18NC):** Mostly null-coalescing fallback mutations (defensive defaults), statement removal on side-effect-free tracking/logging, and 2 confirmed equivalent mutants.

---

## Phase 3c — Test Suite Refactoring ✅ COMPLETE

**Priority: High** — structural cleanup before adding more tests in later phases. Details in `test-refactoring-plan.md`.

- [x] 3c.1 Centralize scattered test entities (`FilterTestEntity`, `UpdateTestEntity`, `KeyConditionTestEntity`, etc.) into `Fixtures/ExpressionTestEntities.cs`
- [x] 3c.2 Consolidate composability tests — abstract base class + two thin derived classes (41 methods → 21 + base). All 41 test executions preserved.
- [x] 3c.3 Split P3MutationKillingTests.cs — deleted P3 file (109 tests), kept pre-existing AttributeValueReaderTests.cs (90) and MappingStrategyTests.cs (12), added 4 unique constructor null-guard tests to DirectResultMapperTests.cs. All 1004 tests pass.
- [x] 3c.4 Distribute P4MutationKillingTests.cs to subsystem files; consolidate CacheStatistics hit-rate tests into [Theory]. Created CacheStatisticsTests.cs (24 executions), InternalRequestExtensionsTests.cs (12). Appended to ExpressionCacheTests (5), DynamoDbExpressionConfigTests (7), AliasGeneratorTests (4), RequestMergeHelpersTests (4), UpdateExtensionsTests (1). Deleted P4 file. 1006 tests pass.
- [x] 3c.5 Consolidate P2 Regions A/B into [Theory], rename to ConverterEdgeCaseTests.cs (68 methods → 54, 68 test executions preserved)
- [x] 3c.6 All 1006 non-property tests pass (+ 102 property = 1108 total). Test execution count preserved.

**Exit criteria ✅**: ~123 fewer test methods with identical test execution count. All tests green. No mutation score regression.

---

## Phase 4 — Contract & Snapshot Testing (PR-05) ✅ COMPLETE

30 snapshot tests covering all expression builder types. Alias scope isolation verified. All `.verified.txt` files committed incrementally (commits `bc77178`–`03d185a`). 1036 non-property tests pass.

- [x] 4.1–4.8 All snapshot tests implemented and committed
- [x] 4.9 All 30 `.verified.txt` files committed
- [x] 4.10 Phase 4 fully committed and pushed

---

## Test Quality Audit ✅ COMPLETE

Full audit of all test projects (~55 files). 45 quality issues fixed (commit `d71e978`), performance fixes applied (pending commit). See `test-quality-audit.md` for full details.

- [x] Deleted 23 redundant duplicate tests (-1,016 lines across 20 files)
- [x] Fixed 12 misleading names / missing key assertions
- [x] Removed or strengthened 10 trivially true / weak assertions
- [x] Replaced `Random.Shared.Next()` with `Gen.Choose`/`Gen.Elements` in FsCheck generators
- [x] Cached 13 `Regex` patterns as `static readonly` with `RegexOptions.Compiled`
- [x] **Fixed `Gen.Where` test host crash** — `ProjectionSelectorGenerator` used `Gen.Where` inside nested `Gen.SelectMany` chains, which crashes the test host in FsCheck 3.0.0-rc3 (StackOverflow in retry/shrink). Replaced with pre-computed unique combinations. Full suite (1,076 tests) now completes in ~1s with clean exit.
- [x] **Implemented `KeyConditionOperationGenerator`** — Replaced `ExpressionGenerators.KeyConditionPredicate` stub (`NotImplementedException`) with `KeyConditionOperation` backed by `KeyConditionOperationGenerator`. Generator produces `Func<KeyConditionExpressionBuilder<TestKeyedEntity>, KeyConditionExpressionResult>` across Simple (PK only), Composite (PK + SK comparison: =, <, <=, >, >=), and Complex (PK + BETWEEN/begins_with) tiers. Added 7 generator smoke tests + 3 generator-based property tests. All generators now have full implementations — property-based testing parity achieved. Test count: 1,086.

---

## Phase 5 — Benchmarking (PR-04) ✅ COMPLETE

**Priority: Medium** — establishes performance baselines after correctness is locked down.

**Results (2026-02-23):**
- All 9 benchmark classes run on .NET 8.0.24 (BenchmarkDotNet 0.14.0)
- Warm path faster than cold: Projection 3x, EndToEnd 1.4x, Cache 4.3x
- Direct result mapping 4.8x faster than hand-written (84ns vs 404ns)
- Largest warm-path allocation: EndToEnd 9.92 KB (< 10KB threshold)
- 9 baseline JSON files committed to `tests/DynamoDb.ExpressionMapping.Benchmarks/baselines/`

- [x] 5.1 Create `tests/DynamoDb.ExpressionMapping.Benchmarks/` project with `BenchmarkDotNet` (>= 0.14.x)
- [x] 5.2 Create `Fixtures/BenchmarkEntities.cs` with representative entity types
- [x] 5.3 Write `ProjectionBuilderBenchmarks` — cold/warm, varying property count, reserved keywords (PR-04.1)
- [x] 5.4 Write `FilterExpressionBenchmarks` — simple through complex predicates (PR-04.2)
- [x] 5.5 Write `FilterCompositionBenchmarks` — And/Or, chaining (PR-04.3)
- [x] 5.6 Write `UpdateExpressionBenchmarks` — single through mixed clauses (PR-04.4)
- [x] 5.7 Write `DirectResultMapperBenchmarks` — compilation + mapping, manual baseline comparison (PR-04.5)
- [x] 5.8 Write `TypeConverterBenchmarks` — per-type conversion (6 types) and resolution (4 paths: exact/nullable/enum/generic-collection) (PR-04.6)
- [x] 5.9 Write `ExpressionCacheBenchmarks` — hit/miss at varying cache sizes, key generation overhead (PR-04.7)
- [x] 5.10 Write `KeyConditionBenchmarks` and `EndToEndBenchmarks` (PR-04.8)
- [x] 5.11 Run all benchmarks, save baseline results as JSON

**Exit criteria**: All benchmarks run on .NET 8. Warm path faster than cold. Result mapping within 2x of hand-written. No warm-path build allocates > 10KB. Baseline JSON committed.

---

## Phase 6 — Code Coverage Enforcement (PR-06)

**Priority: Medium-Low** — CI gate to prevent regression of all the quality work from prior phases.

- [x] 6.1 Create `tests/coverlet.runsettings` with exclusions and format settings (PR-06.2) — verified: 96.25% line, 86.76% branch coverage collected correctly
- [ ] 6.2 Update `ci.yml` to use runsettings and collect Cobertura output (PR-06.1)
- [ ] 6.3 Add ReportGenerator to CI — HTML + MarkdownSummaryGithub + Badges (PR-06.3)
- [ ] 6.4 Add coverage PR comment via `marocchino/sticky-pull-request-comment` (PR-06.3)
- [ ] 6.5 Enforce threshold: 90% line / 85% branch overall (PR-06.4, PR-06.5)
- [ ] 6.6 Apply `[ExcludeFromCodeCoverage]` to excluded areas per PR-06.6
- [ ] 6.7 Verify local `reportgenerator` HTML workflow works

**Exit criteria**: CI fails if coverage drops below thresholds. PR comment shows coverage summary. HTML report available as artifact.

---

## Phase 7 — API Compatibility Tracking (PR-07)

**Priority: Lowest** — protects consumers as library evolves; depends on stable API from all prior phases.

- [ ] 7.1 Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` (3.3.4) to library project (PR-07.1)
- [ ] 7.2 Generate initial `PublicAPI.Shipped.txt` and empty `PublicAPI.Unshipped.txt` for v0.1.x surface (PR-07.2)
- [ ] 7.3 Configure RS0016/RS0017/RS0025/RS0026 as errors in CI (PR-07.1)
- [ ] 7.4 Add API diff PR comment step to `ci.yml` (PR-07.4)
- [ ] 7.5 Add `dotnet-inspect diff --breaking` step to `publish.yml` release pipeline (PR-07.3)

**Exit criteria**: `PublicAPI.Shipped.txt` captures complete API surface. CI fails on undeclared API changes. Release pipeline blocks breaking changes on non-major bumps.

---

## Phase Boundaries

| Boundary     | Rule                                                     |
| ------------ | -------------------------------------------------------- |
| Phase 1 → 2  | Phase 1 fully committed before starting phase 2         |
| Phase 2 → 3a | Phase 2 fully committed before starting phase 3a        |
| Phase 3a → 3b| Phase 3a fully committed before starting phase 3b       |
| Phase 3b → 3c| Phase 3b fully committed before starting phase 3c       |
| Phase 3c → 4 | Phase 3c fully committed before starting phase 4        |
| Phase 4 → 5  | Phase 4 fully committed before starting phase 5         |
| Phase 5 → 6  | Phase 5 fully committed before starting phase 6         |
| Phase 6 → 7  | Phase 6 fully committed before starting phase 7         |
