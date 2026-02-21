# PR-00: Production Readiness Overview

## Purpose

This spec suite defines the testing and validation work required to bring `DynamoDb.ExpressionMapping` v0.1.x to production-grade confidence — comparable to well-established, battle-tested libraries — without waiting for organic production usage to surface issues.

## Current State (v0.1.1)

- 565 unit tests across 11 subsystems
- 68+ integration tests against DynamoDB Local (Testcontainers)
- 2 example applications (ConsoleQuickStart, WebApiExample)
- CI/CD with unit test gate, separate integration pipeline, security scanning
- Coverlet-based code coverage collection (no enforced thresholds)
- No benchmarking, mutation testing, property-based testing, or load testing infrastructure

**Known issue:** Integration tests and unit tests share a single test project (`DynamoDb.ExpressionMapping.Tests`). xUnit eagerly instantiates collection fixtures — including the `DynamoDbFixture` which starts a Testcontainers Docker container — *before* applying test filters. This causes all test runs (including unit-only, Stryker mutation, and coverage) to incur a 10-30+ second Docker startup penalty or hang under resource pressure. Phase 3a addresses this by splitting integration tests into `DynamoDb.ExpressionMapping.IntegrationTests`.

## Spec Index

| Spec  | Title                       | Risk Addressed                                               |
| ----- | --------------------------- | ------------------------------------------------------------ |
| PR-01 | Property-Based Testing      | Unanticipated expression tree inputs                         |
| PR-02 | Soak & Concurrency Testing  | Thread-safety, memory leaks, cache correctness under load    |
| PR-03 | Mutation Testing            | Weak/redundant tests that pass despite source mutations      |
| PR-04 | Benchmarking                | Performance regressions, allocation pressure, cache efficacy |
| PR-05 | Contract & Snapshot Testing | Unintended changes to generated DynamoDB expressions         |
| PR-06 | Code Coverage Enforcement   | Test coverage regression across releases                     |
| PR-07 | API Compatibility Tracking  | Accidental breaking changes to public API surface            |

## Implementation Order

Recommended sequencing based on risk-to-effort ratio:

1. **PR-01** (Property-Based Testing) — highest probability of finding real bugs
2. **PR-02** (Soak & Concurrency) — highest severity if issues exist
3. **Integration Test Isolation** (Phase 3a) — prerequisite: split integration tests into dedicated project to unblock efficient Stryker/coverage/unit-test runs
4. **PR-03** (Mutation Testing) — validates existing test suite quality
5. **PR-05** (Snapshot Testing) — low effort, high regression protection
6. **PR-04** (Benchmarking) — establishes performance baseline
7. **PR-06** (Coverage Enforcement) — CI gate for ongoing quality
8. **PR-07** (API Compatibility) — protects consumers as library evolves

## Success Criteria

The library is considered production-ready when:

- Property-based tests pass at 1,000 cases per invariant (default) and validated at 10,000 via `FSCHECK_MAX_TEST=10000` before phase completion
- Soak tests run 30+ minutes with stable memory and zero errors
- Mutation score exceeds 80% on expression builder subsystems
- Benchmark baselines exist for all hot paths with CI regression gates
- Snapshot tests lock all expression output formats
- Line coverage exceeds 90% with CI enforcement
- Public API surface is tracked and breaking changes require explicit opt-in
