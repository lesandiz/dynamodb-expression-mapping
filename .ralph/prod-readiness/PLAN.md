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

## Phase 3 — Mutation Testing (PR-03)

**STATUS: BLOCKED (Turn 3)** — Stryker 4.12.0 setup issue. See task 3.3 for details.

**Priority: High** — validates that the existing + phase-1 test suite actually catches bugs.

- [x] 3.1 Install `dotnet-stryker` as local tool
- [x] 3.2 Create `stryker-config.json` with thresholds (high: 90, low: 80, break: 75) and mutate/exclude paths (PR-03.1)
- [ ] 3.3 Run initial full mutation analysis — **BLOCKED: Stryker 4.12.0 fails with "No project found" after successful project analysis. Attempted fixes: traditional .sln file creation, .NET 8 SDK via global.json, CLI-only configuration, project path corrections. Root cause: Stryker analyzes both projects successfully but then reports "Analyzing 0 projects". Awaiting human decision on: (1) alternative mutation testing tool, (2) Stryker version change, or (3) different project structure.**
- [ ] 3.4 Analyse Priority 1 subsystems (expression builders) — triage surviving mutants (PR-03.4)
- [ ] 3.5 Write tests to kill surviving non-equivalent mutants in expression builders
- [ ] 3.6 Analyse Priority 2 subsystems (type conversion) — triage and fix
- [ ] 3.7 Analyse Priority 3 subsystems (result mapping) — triage and fix
- [ ] 3.8 Analyse Priority 4 subsystems (supporting systems) — triage and fix
- [ ] 3.9 Re-run full mutation analysis, verify 80%+ on all subsystems, 90%+ on expression builders
- [ ] 3.10 Commit phase 3

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

| Boundary    | Rule                                            |
| ----------- | ----------------------------------------------- |
| Phase 1 → 2 | Phase 1 fully committed before starting phase 2 |
| Phase 2 → 3 | Phase 2 fully committed before starting phase 3 |
| Phase 3 → 4 | Phase 3 fully committed before starting phase 4 |
| Phase 4 → 5 | Phase 4 fully committed before starting phase 5 |
| Phase 5 → 6 | Phase 5 fully committed before starting phase 6 |
| Phase 6 → 7 | Phase 6 fully committed before starting phase 7 |
