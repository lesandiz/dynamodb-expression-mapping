# PR-03: Mutation Testing

## Motivation

Code coverage measures which lines execute during tests — not whether tests would fail if the code were wrong. Mutation testing introduces small, systematic changes (mutations) to the source and checks whether the existing test suite detects them. A mutation that survives (tests still pass) indicates a gap in test quality.

With 565 unit tests, the library has extensive coverage. Mutation testing validates whether that coverage is meaningful.

## Prerequisites

- **Phase 3a (Integration Test Isolation)** must be completed first. Integration tests must be in `DynamoDb.ExpressionMapping.IntegrationTests` so that Stryker only runs unit/property tests and never triggers Docker container startup via xUnit's eager collection fixture initialization.

## Scope

Run mutation testing on the core library (`DynamoDb.ExpressionMapping`) using the unit test suite (`DynamoDb.ExpressionMapping.Tests`). Integration tests are excluded by project separation (not by test filter). Focus analysis on the highest-risk subsystems.

## Dependencies

- **[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/)** (>= 4.x) — .NET mutation testing framework
- Install: `dotnet tool install -g dotnet-stryker` or as a local tool

## Known Issues & Workarounds

### Buildalyzer `TargetFramework` Discovery (Resolved)

Stryker's embedded Buildalyzer library cannot resolve `TargetFramework` when it is only defined in `Directory.Build.props` and not in the individual `.csproj` files. This causes Stryker to report "Analyzing 0 projects" / "No project found" despite successful initial analysis.

**Fix:** Add `<TargetFramework>net8.0</TargetFramework>` explicitly to both `.csproj` files. This is redundant with `Directory.Build.props` but required for Buildalyzer's design-time build.

### MSBuild Version Conflict (Environment-Specific)

On machines with Visual Studio 2019 BuildTools installed alongside VS 2022, Buildalyzer discovers the old MSBuild 16.11.2 first. .NET 8 SDK requires MSBuild >= 17.8.3, causing silent analysis failure.

**Workaround:** Set the `MSBUILD_EXE_PATH` environment variable before running Stryker:

```bash
MSBUILD_EXE_PATH="C:/Program Files/dotnet/sdk/8.0.418/MSBuild.dll" dotnet stryker
```

This does not affect CI (`ubuntu-latest` has no VS 2019 BuildTools). Stryker 4.13.0 (PR #3426, merged Feb 2026) may resolve this automatically.

**Alternative:** Uninstall VS 2019 BuildTools if no longer needed.

## Configuration

### PR-03.1: Stryker Configuration File

```json
// stryker-config.json (repository root)
{
  "$schema": "https://raw.githubusercontent.com/stryker-mutator/stryker-net/master/src/Stryker.Core/Stryker.Core/stryker-config.schema.json",
  "stryker-config": {
    "project": "DynamoDb.ExpressionMapping.csproj",
    "test-projects": [
      "tests/DynamoDb.ExpressionMapping.Tests/DynamoDb.ExpressionMapping.Tests.csproj"
    ],
    "reporters": ["html", "json", "progress"],
    "thresholds": {
      "high": 90,
      "low": 80,
      "break": 75
    },
    "mutate": [
      "src/DynamoDb.ExpressionMapping/**/*.cs",
      "!src/DynamoDb.ExpressionMapping/obj/**",
      "!src/DynamoDb.ExpressionMapping/Attributes/**"
    ],
    "ignore-mutations": [
      "string"
    ],
    "concurrency": 4,
    "solution": "DynamoDb.ExpressionMapping.slnx"
  }
}
```

**Note:** `test-projects` intentionally excludes `DynamoDb.ExpressionMapping.IntegrationTests`. Integration tests are separated into their own project (Phase 3a) so Stryker never triggers Docker container startup via xUnit's eager collection fixture initialization. No `test-case-filter` is needed — project-level isolation is cleaner and more reliable than runtime filtering.

### PR-03.2: Threshold Definitions

| Threshold | Score | Meaning                                            |
| --------- | ----- | -------------------------------------------------- |
| `high`    | 90%   | Mutation score above this is good (green)          |
| `low`     | 80%   | Mutation score below this needs attention (yellow) |
| `break`   | 75%   | Mutation score below this fails the build (red)    |

Target: achieve 80%+ mutation score on all subsystems, 90%+ on expression builders.

## Targeted Subsystems

### Priority 1: Expression Builders (highest mutation risk)

These components contain the most conditional logic and string construction:

| Subsystem                                      | Source Files                                                     | Mutation Focus                                                |
| ---------------------------------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------- |
| `Expressions/FilterExpressionBuilder.cs`       | Comparison operator dispatch, logical combiner, parenthesisation | Operator swaps (`==` ↔ `!=`), boundary mutations (`>` ↔ `>=`) |
| `Expressions/UpdateExpressionBuilder.cs`       | Clause keyword selection, function call generation               | String literal mutations, clause ordering                     |
| `Expressions/KeyConditionExpressionBuilder.cs` | Staged builder validation, operator mapping                      | Guard clause removal, operator swaps                          |
| `Expressions/ProjectionExpressionVisitor.cs`   | Expression tree node dispatch                                    | Switch case removal, early returns                            |

### Priority 2: Type Conversion

| Subsystem                                    | Mutation Focus                                             |
| -------------------------------------------- | ---------------------------------------------------------- |
| `Mapping/Converters/*.cs`                    | Format strings, null handling branches, type dispatch      |
| `Mapping/ExpressionValueEmitter.cs`          | Resolution order logic, fallback chains                    |
| `Mapping/AttributeValueConverterRegistry.cs` | Registration lookup, nullable wrapping, generic resolution |

### Priority 3: Result Mapping

| Subsystem                             | Mutation Focus                                                  |
| ------------------------------------- | --------------------------------------------------------------- |
| `ResultMapping/DirectResultMapper.cs` | Constructor vs property-setter dispatch, nested path navigation |

### Priority 4: Supporting Systems

| Subsystem                                     | Mutation Focus                         |
| --------------------------------------------- | -------------------------------------- |
| `ReservedKeywords/ReservedKeywordRegistry.cs` | Case sensitivity, boundary conditions  |
| `ReservedKeywords/AliasGenerator.cs`          | Counter increment, prefix construction |
| `Caching/ExpressionCache.cs`                  | Cache hit/miss logic                   |
| `Extensions/RequestMergeHelpers.cs`           | Collision detection, dictionary merge  |

## Mutation Types

Stryker.NET applies these mutators by default. Key ones for this library:

| Mutator          | Example                                 | Risk in this library                               |
| ---------------- | --------------------------------------- | -------------------------------------------------- |
| Arithmetic       | `+` → `-`                               | Medium — increment/decrement in update expressions |
| Equality         | `==` → `!=`                             | High — comparison operator dispatch                |
| Logical          | `&&` → `\|\|`                           | High — boolean expression composition              |
| String           | `"SET"` → `""`                          | High — clause keyword generation                   |
| Negate condition | `if (x)` → `if (!x)`                    | High — guard clauses, null checks                  |
| Remove statement | Delete a line                           | Medium — side effects in builders                  |
| Return value     | `return x` → `return default`           | High — converter results                           |
| Linq             | `.Where(...)` → `.Where(...).Reverse()` | Low                                                |

## Execution

### PR-03.3: Running Locally

```bash
# Full mutation run (may take 10-30 minutes depending on project size)
dotnet stryker

# If VS 2019 BuildTools is installed (see Known Issues above):
MSBUILD_EXE_PATH="C:/Program Files/dotnet/sdk/8.0.418/MSBuild.dll" dotnet stryker

# Target specific subsystem
dotnet stryker --mutate "src/DynamoDb.ExpressionMapping/Expressions/**/*.cs"

# Quick smoke run (fewer mutations)
dotnet stryker --since:main

# Exclude slow property tests from initial test verification
dotnet stryker --test-case-filter "Category!=Property"
```

### PR-03.4: Analysing Results

Stryker generates an HTML report at `StrykerOutput/reports/mutation-report.html`:

- **Killed**: Test suite detected the mutation (good)
- **Survived**: Tests passed despite the mutation (test gap)
- **No coverage**: Mutated code not covered by any test
- **Timeout**: Mutation caused infinite loop (treated as killed)

For each surviving mutant, determine:
1. Is the mutation semantically equivalent (same behaviour despite code change)? → Mark as ignored
2. Is there a missing test case? → Write the test
3. Is the code unreachable? → Remove dead code

## CI Integration

### PR-03.5: CI Workflow

Run mutation testing weekly (full) and on PRs (incremental, `--since` mode):

```yaml
# .github/workflows/mutation-testing.yml
name: Mutation Testing
on:
  schedule:
    - cron: '0 4 * * 0'  # Weekly on Sunday 4am
  pull_request:
    paths:
      - 'src/DynamoDb.ExpressionMapping/**'
  workflow_dispatch:

jobs:
  mutate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Needed for --since
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet tool restore
      - name: Run Stryker (incremental on PR, full on schedule)
        run: |
          if [ "${{ github.event_name }}" = "pull_request" ]; then
            dotnet stryker --since:main
          else
            dotnet stryker
          fi
      - uses: actions/upload-artifact@v4
        with:
          name: mutation-report
          path: StrykerOutput/**/reports/
```

### PR-03.6: Break Threshold

The `break: 75` threshold in the Stryker config causes the CI job to exit non-zero if the mutation score falls below 75%. This gates the weekly run but not PRs (incremental runs may have skewed scores).

## Actionable Output

After the initial run, create a tracking issue per subsystem with surviving mutants:

```
## Surviving Mutants — FilterExpressionBuilder

| Line | Mutation                   | Status   | Action                                 |
| ---- | -------------------------- | -------- | -------------------------------------- |
| 142  | `==` → `!=` in VisitBinary | Survived | Add test for inequality operator       |
| 187  | Removed null guard         | Survived | Equivalent mutant — guard is redundant |
| 203  | `"AND"` → `""`             | Survived | Missing test for AND keyword in output |
```

## Success Criteria

- Initial mutation run completes without infrastructure errors
- Expression builder subsystems achieve 80%+ mutation score
- All surviving non-equivalent mutants have corresponding test cases written
- CI weekly run enforces 75% break threshold
- Mutation score trends upward across releases
