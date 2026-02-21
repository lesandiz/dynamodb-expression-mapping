# PR-06: Code Coverage Enforcement

## Motivation

The library collects code coverage via Coverlet in CI but does not enforce thresholds. Coverage can silently regress as new code is added without corresponding tests. Enforcing thresholds in CI prevents this regression and provides visibility into under-tested areas.

## Current State

- Coverlet (v6.0.0) configured in test project
- CI collects coverage with `--collect:"XPlat Code Coverage"`
- Coverage artifacts uploaded to GitHub Actions
- No threshold enforcement
- No coverage reporting tool (e.g., Codecov, ReportGenerator)
- No per-project or per-subsystem visibility

## Implementation

### PR-06.1: Coverage Threshold in CI

Add threshold enforcement to the existing `ci.yml` workflow:

```yaml
# In .github/workflows/ci.yml, update the test step:
- name: Run unit tests with coverage
  run: |
    dotnet test tests/DynamoDb.ExpressionMapping.Tests/ \
      --configuration Release \
      --collect:"XPlat Code Coverage" \
      --settings tests/coverlet.runsettings \
      -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
```

**Note:** No `--filter "Category!=Integration"` is needed — integration tests live in a separate project (`DynamoDb.ExpressionMapping.IntegrationTests`, split in Phase 3a) and are not discovered when running the unit test project.

### PR-06.2: Coverlet RunSettings

```xml
<!-- tests/coverlet.runsettings -->
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Exclude>[DynamoDb.ExpressionMapping.Tests]*</Exclude>
          <ExcludeByAttribute>
            GeneratedCodeAttribute,
            CompilerGeneratedAttribute,
            ExcludeFromCodeCoverageAttribute
          </ExcludeByAttribute>
          <SingleHit>false</SingleHit>
          <UseSourceLink>true</UseSourceLink>
          <IncludeTestAssembly>false</IncludeTestAssembly>
          <SkipAutoProps>true</SkipAutoProps>
          <DeterministicReport>true</DeterministicReport>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### PR-06.3: ReportGenerator Integration

Add [ReportGenerator](https://github.com/danielpalme/ReportGenerator) for human-readable reports:

```yaml
# Add to ci.yml after test step
- name: Install ReportGenerator
  run: dotnet tool install -g dotnet-reportgenerator-globaltool

- name: Generate coverage report
  run: |
    reportgenerator \
      -reports:"**/coverage.cobertura.xml" \
      -targetdir:"coverage-report" \
      -reporttypes:"Html;MarkdownSummaryGithub;Badges" \
      -assemblyfilters:"+DynamoDb.ExpressionMapping"

- name: Publish coverage report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: coverage-report/

- name: Add coverage to PR comment
  if: github.event_name == 'pull_request'
  uses: marocchino/sticky-pull-request-comment@v2
  with:
    path: coverage-report/SummaryGithub.md
```

### PR-06.4: Threshold Enforcement

Two approaches (implement one or both):

**Option A: Coverlet MSBuild thresholds** (simpler, build-time)

```xml
<!-- In DynamoDb.ExpressionMapping.Tests.csproj -->
<PropertyGroup>
  <CollectCoverage>true</CollectCoverage>
  <CoverletOutputFormat>cobertura</CoverletOutputFormat>
  <Threshold>90</Threshold>
  <ThresholdType>line</ThresholdType>
  <ThresholdStat>total</ThresholdStat>
</PropertyGroup>
```

**Option B: CI script enforcement** (more flexible, per-subsystem)

```yaml
- name: Check coverage thresholds
  run: |
    reportgenerator \
      -reports:"**/coverage.cobertura.xml" \
      -targetdir:"coverage-check" \
      -reporttypes:"JsonSummary"

    # Parse and enforce thresholds
    LINE_COVERAGE=$(jq '.summary.linecoverage' coverage-check/Summary.json)
    BRANCH_COVERAGE=$(jq '.summary.branchcoverage' coverage-check/Summary.json)

    echo "Line coverage: ${LINE_COVERAGE}%"
    echo "Branch coverage: ${BRANCH_COVERAGE}%"

    if (( $(echo "$LINE_COVERAGE < 90" | bc -l) )); then
      echo "::error::Line coverage ${LINE_COVERAGE}% is below 90% threshold"
      exit 1
    fi

    if (( $(echo "$BRANCH_COVERAGE < 85" | bc -l) )); then
      echo "::error::Branch coverage ${BRANCH_COVERAGE}% is below 85% threshold"
      exit 1
    fi
```

### PR-06.5: Coverage Targets

| Scope               | Line Coverage | Branch Coverage |
| ------------------- | ------------- | --------------- |
| Overall             | 90%           | 85%             |
| `Expressions/`      | 95%           | 90%             |
| `Mapping/`          | 95%           | 90%             |
| `ResultMapping/`    | 95%           | 90%             |
| `Extensions/`       | 90%           | 85%             |
| `ReservedKeywords/` | 95%           | 90%             |
| `Caching/`          | 90%           | 85%             |

### PR-06.6: Exclusions

Exclude from coverage measurement:

- Attribute classes (`Attributes/`) — marker types with no logic
- `DynamoDbExpressionConfig` builder — configuration wiring
- Exception constructors (base call only, no logic)
- `[ExcludeFromCodeCoverage]` on any generated or trivial code

## Local Development

```bash
# Run unit tests with coverage locally (no Docker required — integration tests are in separate project)
dotnet test tests/DynamoDb.ExpressionMapping.Tests/ \
  --collect:"XPlat Code Coverage" \
  --settings tests/coverlet.runsettings

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:"Html"

# Open in browser
open coverage-report/index.html    # macOS
start coverage-report/index.html   # Windows
```

## Success Criteria

- CI enforces 90% line coverage / 85% branch coverage
- Coverage report published as PR comment on every pull request
- Coverage badge available for README
- ReportGenerator HTML report available as CI artifact
- No subsystem drops below its target (per PR-06.5)
