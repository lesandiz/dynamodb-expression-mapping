# Production Readiness Plan

> Source of truth: `specs/PR-00` through `PR-07`. This file is a task list only — implementation details live in the specs.

---

## Phase 1 — Property-Based Testing (PR-01)

**Priority: Highest** — most likely to surface real bugs in expression builders and converters.

- [x] 1.1 Add `FsCheck.Xunit` (>= 3.x) to test project
- [x] 1.2 Create `PropertyBased/Generators/` folder and `ExpressionGenerators.cs` entry point
- [x] 1.3 Implement `ProjectionSelectorGenerator` (simple, composite, complex tiers)
- [x] 1.4 Implement `FilterPredicateGenerator` (simple, composite, complex tiers)
- [x] 1.5 Implement `UpdateOperationGenerator` (simple, composite, complex tiers)
- [x] 1.6 Create `PropertyTestConfig` with env-var-driven max-test counts (10k local, 1k CI)
- [x] 1.7 Write `ProjectionBuilderProperties` — invariant PR-01.1 (alias prefix, reserved keyword aliasing)
- [x] 1.8 Write `FilterExpressionBuilderProperties` — invariant PR-01.2 (non-empty, balanced parens, placeholder/dictionary consistency, scope isolation)
- [x] 1.9 Write `UpdateExpressionBuilderProperties` — invariant PR-01.3 (well-formed clauses, correct alias prefixes)
- [x] 1.10 Write `KeyConditionBuilderProperties` — invariant PR-01.6 (partition key equality present)
- [x] 1.11 Write `ComposabilityProperties` — invariant PR-01.4 (no alias collisions after composition)
- [x] 1.12 Write `TypeConverterProperties` — invariant PR-01.5 (round-trip, nullable semantics)

**BUG FOUND & FIXED (Task 1.13 - 10k iteration run)**:
- UpdateExpressionBuilder created orphaned placeholders when the same property was set multiple times
- Example: `.Set(x => x.Price, 10).Set(x => x.Price, 20)` generated `:upd_v0` and `:upd_v1` but only `:upd_v1` was used
- Root cause: Operation dictionaries overwrite on duplicate keys, but each call increments alias counters
- Fix applied: Added `RemoveOldValuePlaceholders()` to clean up both value placeholders and name aliases before overwriting
- All methods updated: Set, Increment, Decrement, SetIfNotExists, AppendToList, Add, Delete
- Verified: Property tests pass at 100 iterations

- [x] 1.13 Run full suite at 10k iterations (`FSCHECK_MAX_TEST=10000`), fix any discovered bugs
- [x] 1.14 Commit phase 1

**Exit criteria**: All properties pass at 1k cases (default). Task 1.13 validates at 10k via `FSCHECK_MAX_TEST=10000`. Any bugs found are fixed and documented.

---

## Phase 2 — Soak & Concurrency Testing (PR-02)

**Priority: High** — highest severity if thread-safety or memory issues exist.

- [x] 2.1 Create `tests/DynamoDb.ExpressionMapping.SoakTests/` project with dependencies (Bogus, Spectre.Console, System.Diagnostics.Metrics)
- [x] 2.2 Add `docker-compose.yml` for DynamoDB Local
- [x] 2.3 Implement `MetricsCollector` and `MemoryMonitor` (PR-02.5, PR-02.6)
- [x] 2.4 Implement `SoakTestRunner` with warm-up / sustained / cool-down phases (PR-02.1)
- [x] 2.5 Implement workloads: `ProjectionWorkload`, `FilterWorkload`, `UpdateWorkload`, `KeyConditionWorkload` (PR-02.4)
- [x] 2.6 Implement `MixedWorkload` and `CacheStressWorkload` (PR-02.4)
- [x] 2.7 Implement concurrency model — shared DI instances, configurable worker count (PR-02.2)
- [x] 2.8 Implement CLI interface with `--duration`, `--concurrency`, `--workload` args
- [x] 2.9 Implement Spectre.Console reporting output and exit-code logic
- [x] 2.10 Write concurrency-specific test scenarios (PR-02.7 items 1–5)
- [ ] 2.11 Run 30-minute soak with 16 workers, verify pass criteria
- [ ] 2.12 Commit phase 2

**Exit criteria**: Zero failures across 30min/16 workers. Memory delta < 20%. Cache entry count stabilises.

---

## Phase 3 — Mutation Testing (PR-03)

**Priority: High** — validates that the existing + phase-1 test suite actually catches bugs.

- [ ] 3.1 Install `dotnet-stryker` as local tool
- [ ] 3.2 Create `stryker-config.json` with thresholds (high: 90, low: 80, break: 75) and mutate/exclude paths (PR-03.1)
- [ ] 3.3 Run initial full mutation analysis
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

Each phase is a hard stop. Completing one phase does **not** auto-start the next.
