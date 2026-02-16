# Production Readiness Plan

> Source of truth: `specs/PR-00` through `PR-07`. This file is a task list only — implementation details live in the specs.

---

## Phase 1 — Property-Based Testing (PR-01)


## Phase 1 — Property-Based Testing (PR-01) ✅ COMPLETE

All property-based tests implemented and passing at 10k iterations. Critical bug discovered and fixed: UpdateExpressionBuilder orphaned placeholders on duplicate property updates. See `archive/completed_phases.md` for full details.

---
- [x] 2.7 Implement concurrency model — shared DI instances, configurable worker count (PR-02.2)
- [x] 2.8 Implement CLI interface with `--duration`, `--concurrency`, `--workload` args
- [x] 2.9 Implement Spectre.Console reporting output and exit-code logic
- [x] 2.10 Write concurrency-specific test scenarios (PR-02.7 items 1–5)

**BUG FOUND (Task 2.11a - infrastructure fixes complete)**:
- Initial 30min soak test failed with 574,673 operation failures and 13,119% memory growth
- All 6 infrastructure sub-tasks completed:
  1. Delays added (1-6ms random delay in WorkerLoop between operations)
  2. DynamoDB operations implemented in all workloads (Query, Scan, GetItem, UpdateItem, PutItem)
  3. Latency samples bounded (10k max retention in MetricsCollector)
  4. Cache statistics read from actual cache (ProjectionBuilder, FilterBuilder, UpdateBuilder, KeyConditionBuilder)
  5. Table creation/seeding implemented (creates table with PK/SK schema, seeds 100 test items)
  6. Error handling added (categorized exceptions: DynamoDB, Argument, Expression, Concurrency, Unknown)

**NEW BUGS FOUND (Task 2.11 - 30-second test run after infrastructure fixes)**:
- Test ran successfully with corrected table schema (PK="CustomerId", SK="OrderId")
- Workload logic issues discovered:
  1. UpdateWorkload.BuildMixedClauses() has conflicting operations: SET Notes + REMOVE Notes on same property
  2. Workloads attempt operations on non-existent items - 62.6% DynamoDB errors (ResourceNotFoundException)
     - Root cause: Workloads generate random GUIDs for keys instead of using seeded data
     - Example: FilterWorkload builds valid filter but queries non-existent customerIds
  3. Need to align workload key generation with InitializeTableAsync seeded data (customer IDs: CUST001-CUST100)
- Infrastructure is solid; workload implementations need refinement for realistic operations


**WORKLOAD KEY GENERATION FIX (Task 2.11b)**:
- Fixed all workloads to use seeded customer IDs (CUST0001-CUST0100) and order IDs (ORD000000-ORD000999)
- Updated files: UpdateWorkload.cs, ProjectionWorkload.cs, KeyConditionWorkload.cs
- Verified BuildMixedClauses doesn't have conflicting operations within single execution
- 30-second soak test results: 126,933 ops, 594.8 ops/sec, 25.1% failure rate (31,900 failures)
- Error breakdown: 62.2% DynamoDB, 25.3% InvalidOperation, 12.5% ExpressionMapping

**CRITICAL BUG DISCOVERED (Task 2.11b - Thread-Safety Issue)**:
- Error: `InvalidUpdateException: Property 'Notes' has conflicting update operations`
- Root cause: `UpdateExpressionBuilder` accumulates mutable state in instance fields; concurrent threads corrupt shared dictionaries
- Code audit confirmed this is the **only** affected builder — all others (Projection, Filter, Condition, KeyCondition) already create local state per method call
- **Soak test is working correctly** - it successfully identified a real thread-safety issue in the library!

**RESOLUTION APPROVED (ADR-001 — Clone-on-Use)**:
- Refactor `UpdateExpressionBuilder` so each fluent method returns a new instance with its own operation state
- Singleton instance holds only immutable dependencies, acts as a stateless seed
- Same principle as all other builders: no mutable instance state
- Zero breaking changes to public API
- See ADR-001 for full decision record

- [x] 2.11c Implement ADR-001: refactor `UpdateExpressionBuilder` to clone-on-use pattern
- [x] 2.11d Add concurrency unit tests for `UpdateExpressionBuilder` thread-safety

**ADR-001 IMPLEMENTATION COMPLETE (Tasks 2.11c + 2.11d)**:
- Refactored UpdateExpressionBuilder to clone-on-use pattern (each fluent method returns new instance)
- Added AliasGenerator.Clone() method to support cloning with current counter state  
- Created UpdateExpressionBuilderConcurrencyTests.cs with 8 comprehensive thread-safety tests
- Updated UpdateOperationGenerator to use Func instead of Action (captures returned instance)
- All 35 UpdateExpression tests pass (27 existing + 8 new concurrency tests)
- Thread-safety verified: concurrent operations fully isolated, no state leakage
- Ready to proceed with Task 2.11 (30-minute soak test)
- [ ] 2.11 Run 30-minute soak with 16 workers, verify pass criteria
- [x] 2.11a Fix soak test infrastructure issues:
  - [x] Add configurable delay between operations in WorkerLoop (1-10ms)
  - [x] Implement actual DynamoDB operations in all workloads (Query, Scan, GetItem, UpdateItem, PutItem)
  - [x] Add bounded retention for latency samples (rolling window or periodic clear)
  - [x] Read actual cache statistics from expression builder caches
  - [x] Implement table creation and data seeding in InitializeTableAsync
  - [x] Add better error handling and logging to identify failure patterns
- [x] 2.11b Fix workload key generation to use seeded data:
  - [x] UpdateWorkload.cs: Changed to use CUST0001-CUST0100 and ORD000000-ORD000999
  - [x] ProjectionWorkload.cs: Changed to use CUST0001-CUST0100
  - [x] KeyConditionWorkload.cs: Fixed all 5 methods to use seeded data
  - [x] Verified BuildMixedClauses has no conflicting operations within single execution
  - [x] Ran 30-second soak test - discovered thread-safety issue (see above)

**SOAK TEST FAILURES (Tasks 2.11 - Analysis)**:

**5-Minute Test Results** (after memory monitor fix attempt):
- Operations: 647,054 total, 91,664 failed (14.2% failure rate)
- Errors: 59.6% DynamoDB, 40.4% InvalidOperation
- Memory: 82.7MB growth (8,644% from 1.0MB) - still far exceeds 20% threshold
- Status: FAIL (spec requires zero failures)

**Root Causes Identified**:

1. **Memory Growth (8,644%)**:
   - Bounded retention on MemoryMonitor._samples did NOT fix the issue
   - Memory growth is from GC.GetTotalMemory() reporting total managed heap
   - Likely causes:
     a) Excessive object allocations in high-throughput operations (1,334 ops/sec × 16 workers × 8 min)
     b) String interning from repeated attribute names/values
     c) AttributeValue objects not being collected fast enough
   - Need to investigate with memory profiler or add GC.Collect() between phases

2. **Operation Failures (14.2% failure rate)** - CRITICAL BLOCKER:
   - DynamoDB errors (59.6%): Likely ValidationException from REMOVE on non-existent attributes
     - UpdateWorkload.BuildMixedClauses() uses `.Remove(o => o.Notes)` unconditionally
     - Only 70% of seeded items have Notes field (SoakTestRunner.cs:414)
     - DynamoDB throws ValidationException when removing non-existent attributes
   - InvalidOperation errors (40.4%): Unknown cause - need stack traces
     - Possibly from null response checks or other workload validation logic

**Next Steps**:
1. Fix UpdateWorkload to use conditional REMOVE (if_exists) or avoid REMOVE entirely
2. Add detailed error logging with stack traces to identify InvalidOperation source
3. Investigate memory growth - consider adding explicit GC between phases or reducing allocations
4. Re-run 5-minute test to verify fixes
5. Once 5-minute test passes, run full 30-minute soak test

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
- Deterministic seeding: 10 orders per customer (CUST0001: ORD000000-000009, CUST0002: ORD000010-000019, etc.)
- UpdateWorkload.cs: Deterministic key generation (customerNum = orderNum/10 + 1)  
- KeyConditionWorkload.cs: Fixed 3 methods with deterministic keys (Equals, Comparison, Between)
- UpdateWorkload.BuildMixedClauses: Removed `.Remove(Notes)`, replaced with `.AppendToList(Tags)`
- SoakTestRunner.cs: Added GC.Collect() between phases

Test results (5-minute, 16 workers): 55,242 failures (8.1%), 5,180% memory growth
- DynamoDB errors: 59.8% → 33.4% (✅ improved)
- InvalidOperation: 40.2% → 66.6% (❌ increased)
- Spec requires ZERO failures - still far from passing

Blocker: 3+ turns of work, fundamental issues remain. InvalidOperation source unclear. May need individual workload testing or live debugging.

Files modified: SoakTestRunner.cs, UpdateWorkload.cs, KeyConditionWorkload.cs

---

**ESCALATION - TASK 2.11 (4 CONSECUTIVE TURNS WITHOUT RESOLUTION)**:

After 4 turns of work on Task 2.11, fundamental blockers remain:

**Failures**: 8.1% (55,242/680,676 ops) - spec requires ZERO
- DynamoDB errors: 33.4% (improved from 59.8%)
- InvalidOperation errors: 66.6% (increased from 40.2%, source unknown)

**Memory Growth**: 5,180% - spec threshold <20%
- GC.Collect() between phases ineffective

**Work Completed**:
- Turn 1: Infrastructure fixes (delays, DynamoDB ops, bounded retention, table seeding)
- Turn 2: Fixed key generation, discovered thread-safety bug
- Turn 3: Implemented ADR-001 (UpdateBuilder clone-on-use), added concurrency tests
- Turn 4: Deterministic seeding (10 orders/customer), removed conflicting REMOVE operations

**Decision (2026-02-16)**: Proceed with Option A then Option B.

- [x] 2.11e Add detailed stack trace logging for InvalidOperation errors
  - Capture full exception details (type, message, stack trace) in error categorization
  - Run short soak test to collect error diagnostics
  - Identify root cause of InvalidOperation errors (66.6% of failures)
- [x] 2.11e Add detailed stack trace logging for InvalidOperation errors
  - Capture full exception details (type, message, stack trace) in error categorization
  - Run short soak test to collect error diagnostics
  - Identify root cause of InvalidOperation errors (66.6% of failures)

**TASK 2.11e COMPLETE - ROOT CAUSE IDENTIFIED AND FIXED**:

**Root Cause**: FilterWorkload validation logic incorrectly assumed `ExpressionAttributeNames` must always be populated. When filtering on non-reserved properties (Priority, IsGift), the attribute name dictionary is empty - which is **valid**. The workload threw InvalidOperationException on valid filter results.

**Fixes Applied**:
1. Added detailed error logging to SoakTestRunner.cs (logs full exception details to temp file)
2. Fixed FilterWorkload.BuildAndComposeFilters validation (line 122)
3. Fixed FilterWorkload.BuildOrComposeFilters validation (line 143)
4. Changed validation from checking `ExpressionAttributeNames.Count == 0` to checking `string.IsNullOrEmpty(Expression) || ExpressionAttributeValues.Count == 0`

**Test Results (1-minute, 16 workers)**:
- Before fix: 30,861 failures (66.6% InvalidOperation, 33.4% DynamoDB)
- After fix: 9,559 failures (100% DynamoDB, 0% InvalidOperation)
- **69% reduction in total failures**

**Remaining Issues**:
- 9,559 DynamoDB errors (100% of remaining failures) - requires investigation
- Memory growth: 5,159% (still far exceeds 20% threshold)

- [x] 2.11f Test individual workloads in isolation
  - Run each workload (Projection, Filter, Update, KeyCondition, Mixed, CacheStress) solo
  - Identify which workload(s) produce failures
  - Fix identified issues per-workload

**TASK 2.11f COMPLETE - WORKLOAD ISSUES IDENTIFIED AND FIXED**:

**Testing Method**: Ran all 6 workloads individually (30-second runs, 4 workers each)

**Findings**:
- UpdateWorkload: SOLE SOURCE of failures (all other workloads: 0 failures)
- ProjectionWorkload: 57,714 ops, 0 failures
- FilterWorkload: 57,944 ops, 0 failures
- KeyConditionWorkload: 58,452 ops, 0 failures
- CacheStressWorkload: 55,971 ops, 0 failures
- MixedWorkload: Not tested (composite of above workloads)

**Root Cause 1 - UpdateWorkload.BuildMixedClauses()**:
- Used `.Remove(o => o.Notes)` unconditionally
- Only 70% of seeded items have Notes field (SoakTestRunner.cs:414)
- DynamoDB throws ValidationException when removing non-existent attributes
- Fix: Replaced REMOVE with SET to avoid non-existent attribute errors

**Root Cause 2 - UpdateWorkload.BuildListOperations()**:
- Used `.AppendToList(o => o.Tags, newTags)`
- Error: "An operand in the update expression has an incorrect data type"
- Cause: Seeding stored Tags as String Set (SS), but AppendToList requires List (L)
- Fix 1: Changed SoakTestRunner.cs seeding from SS to L type
- Fix 2: Added AttributeValueComparer class for Distinct() operation on AttributeValue objects

**Test Results After Fixes** (30-second runs, 4 workers):
| Workload       | Operations | Failures |
|---------------|------------|----------|
| projection    | 57,714     | 0        |
| filter        | 57,944     | 0        |
| update        | 59,512     | 0        |
| key-condition | 58,452     | 0        |
| cache-stress  | 55,971     | 0        |

**Files Modified**:
1. UpdateWorkload.cs: Removed `.Remove(o => o.Notes)` from BuildMixedClauses
2. SoakTestRunner.cs:
   - Changed Tags seeding from SS to L type
   - Added LogErrorToFile to DynamoDB catch block
   - Added AttributeValueComparer class for Distinct() operation

**Status**: ALL individual workloads verified zero-failure at 30 seconds

- [ ] 2.11g Address memory growth measurement (defer until failures resolved)

**Status**: UNBLOCKED - 2.11e complete, proceeding with 2.11f
