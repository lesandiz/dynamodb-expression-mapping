# PR-07: API Compatibility Tracking

## Motivation

As a library consumed by other projects, unintentional breaking changes to the public API surface erode consumer trust. API compatibility tracking ensures that:
- Breaking changes are deliberate and documented
- Consumers can confidently upgrade minor/patch versions without fear of compilation failures
- The library follows semantic versioning accurately

## Scope

Track the public API surface of `DynamoDb.ExpressionMapping` and detect changes (additions, removals, modifications) between versions. Gate breaking changes in CI.

## Approach Options

Two complementary approaches — implement both for maximum coverage.

### Approach A: Microsoft.CodeAnalysis.PublicApiAnalyzers (Compile-Time)

Roslyn-based analyser that tracks public API in checked-in text files. Any public API change causes a compiler warning/error until the developer explicitly acknowledges it.

### Approach B: dotnet-inspect diff (Release-Time)

Uses the `dotnet-inspect` CLI tool to compare the API surface between NuGet package versions. Run during release pipeline to generate a human-readable diff.

## Implementation

### PR-07.1: PublicApiAnalyzers Setup

```xml
<!-- Add to DynamoDb.ExpressionMapping.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.3.4">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>

<!-- Treat API changes as build errors in CI -->
<PropertyGroup Condition="'$(CI)' == 'true'">
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <WarningsAsErrors>RS0016;RS0017;RS0022;RS0024;RS0025;RS0026</WarningsAsErrors>
</PropertyGroup>
```

Relevant diagnostics:

| ID     | Description                                         | Severity              |
| ------ | --------------------------------------------------- | --------------------- |
| RS0016 | Add public types/members to API tracking file       | Warning → Error in CI |
| RS0017 | Remove deleted types/members from API tracking file | Warning → Error in CI |
| RS0022 | Constructor made externally visible                 | Warning               |
| RS0024 | Public API symbol added without ship annotation     | Warning               |
| RS0025 | Public API symbol removed                           | Warning → Error in CI |
| RS0026 | Public API symbol changed                           | Warning → Error in CI |

### PR-07.2: API Tracking Files

The analyser uses two text files per project:

```
src/DynamoDb.ExpressionMapping/
├── PublicAPI.Shipped.txt       # API surface of the last shipped version
└── PublicAPI.Unshipped.txt     # API changes since last ship (pending)
```

**Initial generation** — run once to capture the current v0.1.1 API surface:

```bash
# Build to generate the initial PublicAPI.Shipped.txt
# The analyser will emit RS0016 for every public symbol
# Use the code fix to populate the file
dotnet build /p:GeneratePublicApiFiles=true
```

**Workflow for developers:**

1. Add a new public method → RS0016 fires → add to `PublicAPI.Unshipped.txt`
2. Remove a public method → RS0017 fires → remove from tracking file
3. On release → move `Unshipped.txt` contents to `Shipped.txt`

### PR-07.3: dotnet-inspect Diff in Release Pipeline

Add API diff to the publish workflow:

```yaml
# In .github/workflows/publish.yml, before pack step:
- name: API compatibility check
  run: |
    # Get the previous released version
    PREV_VERSION=$(dotnet-inspect DynamoDb.ExpressionMapping --versions --json | jq -r '.[0]')

    # Compare current build against previous release
    dotnet-inspect diff "DynamoDb.ExpressionMapping@${PREV_VERSION}..current" \
      --breaking \
      --stat

    # Fail if breaking changes detected on minor/patch bump
    BREAKING_COUNT=$(dotnet-inspect diff "DynamoDb.ExpressionMapping@${PREV_VERSION}..current" --breaking --json | jq '.breakingChanges | length')

    if [ "$BREAKING_COUNT" -gt 0 ]; then
      echo "::warning::$BREAKING_COUNT breaking changes detected"

      # Check if this is a major version bump
      CURRENT_MAJOR=$(echo "${{ github.ref_name }}" | sed 's/v//' | cut -d. -f1)
      PREV_MAJOR=$(echo "$PREV_VERSION" | cut -d. -f1)

      if [ "$CURRENT_MAJOR" = "$PREV_MAJOR" ]; then
        echo "::error::Breaking changes on non-major version bump. Bump major version or remove breaking changes."
        exit 1
      fi
    fi
```

### PR-07.4: PR API Diff Comment

Show API changes in pull request comments:

```yaml
# In .github/workflows/ci.yml
- name: API diff
  if: github.event_name == 'pull_request'
  run: |
    # Check if PublicAPI.Unshipped.txt has changes
    if git diff --name-only origin/main...HEAD | grep -q "PublicAPI"; then
      echo "## API Changes" > api-diff.md
      echo '```' >> api-diff.md
      git diff origin/main...HEAD -- '**/PublicAPI.Unshipped.txt' >> api-diff.md
      echo '```' >> api-diff.md
    fi

- name: Comment API changes
  if: github.event_name == 'pull_request' && hashFiles('api-diff.md') != ''
  uses: marocchino/sticky-pull-request-comment@v2
  with:
    header: api-changes
    path: api-diff.md
```

### PR-07.5: API Surface Documentation

On each release, generate a public API report:

```bash
# Generate full API surface for the release
dotnet-inspect api DynamoDb.ExpressionMapping > docs/api-surface-v0.1.1.txt

# Generate diff from previous version
dotnet-inspect diff "DynamoDb.ExpressionMapping@0.1.0..0.1.1" > docs/api-diff-v0.1.0-to-v0.1.1.txt
```

## Semantic Versioning Rules

Enforce via CI:

| Change Type                         | Version Bump | Example                              |
| ----------------------------------- | ------------ | ------------------------------------ |
| New public type/member              | Minor        | Add `WithTimeout()` extension method |
| New optional parameter with default | Minor        | Add `bool lenient = false`           |
| Bug fix, internal change            | Patch        | Fix alias generation for edge case   |
| Remove public type/member           | **Major**    | Remove `WithProjection()` overload   |
| Change return type                  | **Major**    | `void` → `Task`                      |
| Change parameter type               | **Major**    | `string` → `ReadOnlySpan<char>`      |
| Make type sealed                    | **Major**    | `class Foo` → `sealed class Foo`     |

## File Structure

```
src/DynamoDb.ExpressionMapping/
├── PublicAPI.Shipped.txt
└── PublicAPI.Unshipped.txt
```

## Success Criteria

- `PublicAPI.Shipped.txt` captures the complete v0.1.1 API surface
- CI fails on undeclared public API changes (RS0016/RS0017 as errors)
- PR comments show API diff when public surface changes
- Release pipeline blocks breaking changes on non-major version bumps
- API surface docs generated per release
