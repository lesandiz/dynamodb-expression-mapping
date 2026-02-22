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

## Phase 3b — Mutation Testing (PR-03)

**STATUS: IN PROGRESS** — Initial analysis complete (3b.4 done). Triage and test writing next.

**Priority: High** — validates that the existing + phase-1 test suite actually catches bugs.

**Resolution history:**

1. **(2026-02-16) Buildalyzer fix:** Stryker's embedded Buildalyzer cannot resolve `TargetFramework` when it is defined only in `Directory.Build.props`. Added explicit `<TargetFramework>net8.0</TargetFramework>` to both `.csproj` files.
2. **(2026-02-16) MSBuild version conflict (env-specific):** On machines with VS 2019 BuildTools installed alongside VS 2022, Buildalyzer discovers the old MSBuild 16.11.2 first. Workaround: `MSBUILD_EXE_PATH="C:/Program Files/dotnet/sdk/8.0.418/MSBuild.dll" dotnet stryker`. Does not affect CI.
3. **(2026-02-22) Integration test isolation:** Stryker in solution mode ignores `test-projects` filter and discovers all test projects from the solution, causing Docker/Testcontainers to spin up for every mutation. Fix: created `DynamoDb.ExpressionMapping.Stryker.sln` excluding the integration test project. Also added `"test-case-filter": "Category!=Property"` to skip slow property tests during mutation runs.
4. **(2026-02-22) Mutate glob path fix:** `mutate` patterns were repo-relative (`src/DynamoDb.ExpressionMapping/**/*.cs`) but Stryker matches against source-project-relative paths. Changed to `**/*.cs` with `!**/Attributes/**` exclusion.

- [x] 3b.1 Install `dotnet-stryker` as local tool
- [x] 3b.2 Create `stryker-config.json` with thresholds (high: 90, low: 80, break: 75) and mutate/exclude paths (PR-03.1)
- [x] 3b.3 Fix Stryker project discovery — added `<TargetFramework>` to both `.csproj` files for Buildalyzer compatibility
- [x] 3b.4 Run initial full mutation analysis — **66.5% overall** (801 tested, 634 killed, 167 survived, 152 NoCoverage). See `mutation-analysis.md` for full breakdown.
- [x] 3b.5 Analyse Priority 1 subsystems — triage complete. 131 mutants (58S+73NC), 2 equivalent, ~107 killable, ~66 tests needed. See scratchpad/p1-mutant-triage.md.
- [ ] 3b.6 Write tests to kill surviving non-equivalent mutants in expression builders — **NEXT PRIORITY**
**3b.6 Test categories (from 3b.5 triage):**
| # | Category | Files | Mutants | Tests |
|---|----------|-------|---------|-------|
| A | Null guard constructor tests | FilterExpressionVisitor, *Result, ProjectionBuilder/Result | 18 | ~15 |
| B | Null property expression (ThrowIfNull) | UpdateExpressionBuilder fluent methods | 19 | ~8 |
| C | Dedup/orphan cleanup (Set same prop twice) | UpdateExpressionBuilder | 6 | ~3 |
| D | Conflict validation (Set+Remove etc.) | UpdateExpressionBuilder L511-520 | 6 | ~6 |
| E | ReAlias OrderByDescending (multi-digit) | Filter/ConditionExpressionResult L130/140 | 4 | ~2 |
| F | Logical mutations in method dispatch | FilterExpressionVisitor L140/154/156/173 | 4 | ~4 |
| G | Bool negation value check | FilterExpressionVisitor L97 | 1 | ~1 |
| H | Closure field/property capture | FilterExpressionVisitor L464/474 | 2 | ~2 |
| I | NoCoverage error/edge paths | FilterExpressionVisitor (Convert, null-from-left, instance Contains) | 26 | ~12 |
| J | SortKeyCondition null/boundary | SortKeyConditionBuilder L103-108/134/164 | 14 | ~8 |
| K | Update misc (alias, empty Build, regex) | UpdateExpressionBuilder L349/500/540/550 | 4 | ~3 |
| L | ProjectionBuilder Lenient mode | ProjectionBuilder L105/117 | 3 | ~2 |
Equivalent (no test): MaxAliasIndex idx>max to idx>=max (2 mutants in Condition/FilterExpressionResult L165)
- [ ] 3b.7 Analyse Priority 2 subsystems (type conversion) — triage and fix
- [ ] 3b.8 Analyse Priority 3 subsystems (result mapping) — triage and fix
- [ ] 3b.9 Analyse Priority 4 subsystems (supporting systems) — triage and fix
- [ ] 3b.10 Re-run full mutation analysis, verify 80%+ on all subsystems, 90%+ on expression builders
- [ ] 3b.11 Commit phase 3b

**Initial scores vs targets:**

| Subsystem | Current | Target |
|---|---|---|
| Expressions | 68.4% | **90%** |
| Mapping | 74.0% | 80% |
| Extensions | 69.7% | 80% |
| Caching | 55.4% | 80% |
| ResultMapping | 38.9% | 80% |
| Root | 85.3% | 80% ✅ |
| Exceptions | 100% | — ✅ |
| ReservedKeywords | 100% | — ✅ |

**Exit criteria**: Mutation score ≥ 80% overall, ≥ 90% expression builders. All surviving non-equivalent mutants addressed.

---

## Phase 4 — Contract & Snapshot Testing (PR-05)

**Priority: Medium-High** — low effort, high regression protection. Done before benchmarking because output stability matters more.

- [ ] 4.1 Add `Verify.Xunit` (>= 26.x) to test project
- [ ] 4.2 Create `Snapshots/ExpressionResultSerializer.cs` or configure Verify's built-in serialiser with `AttributeValue` converter (PR-05.1, PR-05.2)
- [ ] 4.3 Write projection snapshot tests (PR-05.3 — 7 cases)
- [ ] 4.4 Write filter snapshot tests (PR-05.4 — 8 cases)
- [ ] 4.5 Write update snapshot tests (PR-05.5 — 5 cases)
- [ ] 4.6 Write key condition snapshot tests (PR-05.6 — 5 cases)
- [ ] 4.7 Write condition snapshot tests (PR-05.7 — 2 cases)
- [ ] 4.8 Write combined expression snapshot tests (PR-05.8)
- [ ] 4.9 Review and commit all `.verified.txt` files
- [ ] 4.10 Commit phase 4

**Exit criteria**: ≥ 25 snapshots committed. All expression builder types covered. Alias scope isolation verified in combined snapshots.

---

## Phase 5 — Benchmarking (PR-04)

**Priority: Medium** — establishes performance baselines after correctness is locked down.

- [ ] 5.1 Create `tests/DynamoDb.ExpressionMapping.Benchmarks/` project with `BenchmarkDotNet` (>= 0.14.x)
- [ ] 5.2 Create `Fixtures/BenchmarkEntities.cs` with representative entity types
- [ ] 5.3 Write `ProjectionBuilderBenchmarks` — cold/warm, varying property count, reserved keywords (PR-04.1)
- [ ] 5.4 Write `FilterExpressionBenchmarks` — simple through complex predicates (PR-04.2)
- [ ] 5.5 Write `FilterCompositionBenchmarks` — And/Or, chaining (PR-04.3)
- [ ] 5.6 Write `UpdateExpressionBenchmarks` — single through mixed clauses (PR-04.4)
- [ ] 5.7 Write `DirectResultMapperBenchmarks` — compilation + mapping, manual baseline comparison (PR-04.5)
- [ ] 5.8 Write `TypeConverterBenchmarks` — per-type conversion and resolution (PR-04.6)
- [ ] 5.9 Write `ExpressionCacheBenchmarks` — hit/miss at varying cache sizes (PR-04.7)
- [ ] 5.10 Write `KeyConditionBenchmarks` and `EndToEndBenchmarks` (PR-04.8)
- [ ] 5.11 Run all benchmarks, save baseline results as JSON
- [ ] 5.12 Commit phase 5

**Exit criteria**: All benchmarks run on .NET 8. Warm path faster than cold. Result mapping within 2x of hand-written. No warm-path build allocates > 10KB. Baseline JSON committed.

---

## Phase 6 — Code Coverage Enforcement (PR-06)

**Priority: Medium-Low** — CI gate to prevent regression of all the quality work from prior phases.

- [ ] 6.1 Create `tests/coverlet.runsettings` with exclusions and format settings (PR-06.2)
- [ ] 6.2 Update `ci.yml` to use runsettings and collect Cobertura output (PR-06.1)
- [ ] 6.3 Add ReportGenerator to CI — HTML + MarkdownSummaryGithub + Badges (PR-06.3)
- [ ] 6.4 Add coverage PR comment via `marocchino/sticky-pull-request-comment` (PR-06.3)
- [ ] 6.5 Enforce threshold: 90% line / 85% branch overall (PR-06.4, PR-06.5)
- [ ] 6.6 Apply `[ExcludeFromCodeCoverage]` to excluded areas per PR-06.6
- [ ] 6.7 Verify local `reportgenerator` HTML workflow works
- [ ] 6.8 Commit phase 6

**Exit criteria**: CI fails if coverage drops below thresholds. PR comment shows coverage summary. HTML report available as artifact.

---

## Phase 7 — API Compatibility Tracking (PR-07)

**Priority: Lowest** — protects consumers as library evolves; depends on stable API from all prior phases.

- [ ] 7.1 Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` (3.3.4) to library project (PR-07.1)
- [ ] 7.2 Generate initial `PublicAPI.Shipped.txt` and empty `PublicAPI.Unshipped.txt` for v0.1.x surface (PR-07.2)
- [ ] 7.3 Configure RS0016/RS0017/RS0025/RS0026 as errors in CI (PR-07.1)
- [ ] 7.4 Add API diff PR comment step to `ci.yml` (PR-07.4)
- [ ] 7.5 Add `dotnet-inspect diff --breaking` step to `publish.yml` release pipeline (PR-07.3)
- [ ] 7.6 Commit phase 7

**Exit criteria**: `PublicAPI.Shipped.txt` captures complete API surface. CI fails on undeclared API changes. Release pipeline blocks breaking changes on non-major bumps.

---

## Phase Boundaries

| Boundary     | Rule                                                     |
| ------------ | -------------------------------------------------------- |
| Phase 1 → 2  | Phase 1 fully committed before starting phase 2         |
| Phase 2 → 3a | Phase 2 fully committed before starting phase 3a        |
| Phase 3a → 3b| Phase 3a fully committed before starting phase 3b       |
| Phase 3b → 4 | Phase 3b fully committed before starting phase 4        |
| Phase 4 → 5  | Phase 4 fully committed before starting phase 5         |
| Phase 5 → 6  | Phase 5 fully committed before starting phase 6         |
| Phase 6 → 7  | Phase 6 fully committed before starting phase 7         |
