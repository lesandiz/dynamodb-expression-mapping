# PR-05: Contract & Snapshot Testing

## Motivation

The library's output — DynamoDB expression strings, `ExpressionAttributeNames`, and `ExpressionAttributeValues` dictionaries — is a contract with AWS. Any unintended change in the generated output (even if functionally equivalent) can cause confusion, break consumer snapshot tests, or introduce subtle runtime differences.

Current unit tests assert specific outputs, but they are scattered across many test files and don't provide a centralised "golden file" view of what the library produces. Snapshot testing locks down the exact output for representative inputs, making any change — intentional or accidental — immediately visible in diffs.

## Scope

Snapshot-test the complete output (`ExpressionString`, `ExpressionAttributeNames`, `ExpressionAttributeValues`) for a curated set of representative inputs across all expression builders.

## Dependencies

- **[Verify.Xunit](https://www.nuget.org/packages/Verify.Xunit)** (>= 26.x) — snapshot testing framework for xUnit
- Add to `DynamoDb.ExpressionMapping.Tests.csproj`

## Approach

### PR-05.1: Snapshot Serialisation

Create a consistent serialisation format for expression results:

```csharp
public static class ExpressionResultSerializer
{
    /// <summary>
    /// Serialises any expression result to a deterministic, human-readable format
    /// suitable for snapshot comparison.
    /// </summary>
    public static string Serialize(ProjectionResult result)
    {
        // Output format:
        // Expression: Id, #proj_0, Address.City
        // ExpressionAttributeNames:
        //   #proj_0 → Name
        // Paths:
        //   Id (Shape: SingleProperty)
        //   Name (Shape: SingleProperty)
        //   Address.City (Shape: SingleProperty)
    }

    public static string Serialize(FilterExpressionResult result) { ... }
    public static string Serialize(ConditionExpressionResult result) { ... }
    public static string Serialize(UpdateExpressionResult result) { ... }
    public static string Serialize(KeyConditionExpressionResult result) { ... }
}
```

Alternatively, configure Verify's built-in serializer with custom converters for `AttributeValue` types.

### PR-05.2: Verify Configuration

```csharp
// In ModuleInitializer or test assembly setup
public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Init()
    {
        // Sort dictionary keys for deterministic output
        VerifierSettings.SortPropertiesAlphabetically();

        // Custom converter for AttributeValue
        VerifierSettings.AddExtraSettings(settings =>
        {
            settings.Converters.Add(new AttributeValueJsonConverter());
        });

        // Use .verified.txt extension
        VerifierSettings.UseExtension("txt");
    }
}
```

## Snapshot Test Cases

### PR-05.3: Projection Snapshots

```csharp
[UsesVerify]
public class ProjectionSnapshotTests
{
    [Fact]
    public Task SingleProperty()
        => Verify(Build(x => x.Id));

    [Fact]
    public Task MultipleProperties_AnonymousType()
        => Verify(Build(x => new { x.Id, x.Name, x.Count }));

    [Fact]
    public Task NestedProperty()
        => Verify(Build(x => new { x.Id, x.Address.City }));

    [Fact]
    public Task DeeplyNestedProperty()
        => Verify(Build(x => x.Contact.MailingAddress.PostCode));

    [Fact]
    public Task ReservedKeywords()
        => Verify(Build(x => new { x.Name, x.Status }));

    [Fact]
    public Task RemappedAttribute()
        => Verify(Build(x => x.CustomerId));

    [Fact]
    public Task MixedReservedAndRemapped()
        => Verify(Build(x => new { x.Name, x.CustomerId, x.Address.City }));
}
```

### PR-05.4: Filter Snapshots

```csharp
[UsesVerify]
public class FilterSnapshotTests
{
    [Fact]
    public Task SimpleEquality()
        => Verify(Build(x => x.Name == "Alice"));

    [Fact]
    public Task CompoundAndOr()
        => Verify(Build(x => (x.Name == "Alice" && x.Count > 5) || x.Enabled));

    [Fact]
    public Task NullCheck()
        => Verify(Build(x => x.OptionalScore == null));

    [Fact]
    public Task StringFunctions()
        => Verify(Build(x => x.Name.StartsWith("A") && x.Name.Contains("li")));

    [Fact]
    public Task EnumComparison()
        => Verify(Build(x => x.Status == TestStatus.Active));

    [Fact]
    public Task NestedProperty()
        => Verify(Build(x => x.Address.City == "London"));

    [Fact]
    public Task ComposedAnd()
    {
        var left = Build(x => x.Name == "Alice");
        var right = Build(x => x.Count > 5);
        return Verify(left.And(right));
    }

    [Fact]
    public Task ComposedOr()
    {
        var left = Build(x => x.Enabled);
        var right = Build(x => x.Status == TestStatus.Active);
        return Verify(left.Or(right));
    }
}
```

### PR-05.5: Update Snapshots

```csharp
[UsesVerify]
public class UpdateSnapshotTests
{
    [Fact]
    public Task SingleSet()
        => Verify(Build(b => b.Set(x => x.Name, "Bob")));

    [Fact]
    public Task MultipleSetAndRemove()
        => Verify(Build(b =>
        {
            b.Set(x => x.Name, "Bob");
            b.Set(x => x.Count, 42);
            b.Remove(x => x.OptionalScore);
        }));

    [Fact]
    public Task IncrementAndAppend()
        => Verify(Build(b =>
        {
            b.Increment(x => x.Count, 1);
            b.AppendToList(x => x.Tags, new[] { "new" });
        }));

    [Fact]
    public Task SetIfNotExists()
        => Verify(Build(b => b.SetIfNotExists(x => x.Name, "default")));

    [Fact]
    public Task AllClauseTypes()
        => Verify(Build(b =>
        {
            b.Set(x => x.Name, "Updated");
            b.Remove(x => x.OptionalScore);
            b.Add(x => x.Count, 1);
            b.Delete(x => x.Categories, new HashSet<string> { "old" });
        }));
}
```

### PR-05.6: Key Condition Snapshots

```csharp
[UsesVerify]
public class KeyConditionSnapshotTests
{
    [Fact]
    public Task PartitionKeyOnly()

    [Fact]
    public Task PartitionKeyAndSortKeyEquals()

    [Fact]
    public Task PartitionKeyAndSortKeyBetween()

    [Fact]
    public Task PartitionKeyAndSortKeyBeginsWith()

    [Fact]
    public Task ReservedKeywordAttributes()
}
```

### PR-05.7: Condition Snapshots

```csharp
[UsesVerify]
public class ConditionSnapshotTests
{
    [Fact]
    public Task ItemNotExists()
        => Verify(Build(x => DynamoDbFunctions.AttributeNotExists(x.Id)));

    [Fact]
    public Task CompoundCondition()
        => Verify(Build(x => x.Status == TestStatus.Active && x.Count > 0));
}
```

### PR-05.8: Combined Expression Snapshots

```csharp
[UsesVerify]
public class CombinedExpressionSnapshotTests
{
    [Fact]
    public Task QueryRequest_KeyCondition_Projection_Filter()
    {
        // Build all three expression types and apply to a QueryRequest
        // Snapshot the complete request state:
        // - KeyConditionExpression
        // - ProjectionExpression
        // - FilterExpression
        // - ExpressionAttributeNames (merged)
        // - ExpressionAttributeValues (merged)
    }
}
```

## File Structure

```
DynamoDb.ExpressionMapping.Tests/
├── Snapshots/
│   ├── ExpressionResultSerializer.cs
│   ├── ProjectionSnapshotTests.cs
│   ├── FilterSnapshotTests.cs
│   ├── UpdateSnapshotTests.cs
│   ├── KeyConditionSnapshotTests.cs
│   ├── ConditionSnapshotTests.cs
│   └── CombinedExpressionSnapshotTests.cs
```

Verified files (auto-generated by Verify):
```
DynamoDb.ExpressionMapping.Tests/
├── Snapshots/
│   ├── ProjectionSnapshotTests.SingleProperty.verified.txt
│   ├── ProjectionSnapshotTests.MultipleProperties_AnonymousType.verified.txt
│   ├── FilterSnapshotTests.SimpleEquality.verified.txt
│   └── ...
```

## Workflow

### Updating Snapshots

When output intentionally changes (e.g., new alias prefix, formatting change):

```bash
# Review changes
dotnet test --filter "Snapshots" -- RunConfiguration.UpdateSnapshots=true

# Or use Verify's CLI tool
dotnet verify review
```

The `.verified.txt` files are committed to source control. Any unintended change shows as a diff in PRs.

### PR Review

Snapshot diffs in PRs provide reviewers with a clear view of exactly what changed in the library's output, without needing to understand the implementation details.

## Success Criteria

- Snapshot tests cover all expression builder types
- At least 25 representative snapshots across all builders
- Snapshots committed to source control and reviewed in PRs
- Any output format change requires explicit snapshot update (visible in PR diff)
- Combined expression snapshots verify alias scope isolation
