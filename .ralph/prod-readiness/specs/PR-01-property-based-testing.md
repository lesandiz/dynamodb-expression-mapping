# PR-01: Property-Based Testing

## Motivation

Unit tests only verify cases the author anticipated. The library's primary surface — expression tree traversal and transformation — accepts arbitrary LINQ expressions from consumers. Property-based testing generates thousands of random inputs and verifies that invariants hold across all of them, catching edge cases that hand-written tests miss.

## Scope

Expression builders and the type converter system are the primary targets. These components transform unbounded user input (expression trees, .NET values) into structured output (DynamoDB expression strings, `AttributeValue` dictionaries).

## Dependencies

- **[FsCheck.Xunit](https://www.nuget.org/packages/FsCheck.Xunit)** (>= 3.x) — property-based testing framework with xUnit integration
- Add to `DynamoDb.ExpressionMapping.Tests.csproj`

## Test Infrastructure

### Custom Generators

Build `Arbitrary<T>` instances that produce valid expression trees for each builder:

```csharp
public static class ExpressionGenerators
{
    // Generates random property selectors for TestEntity
    public static Arbitrary<Expression<Func<TestEntity, object>>> ProjectionSelectors();

    // Generates random predicates over TestEntity properties
    public static Arbitrary<Expression<Func<TestEntity, bool>>> FilterPredicates();

    // Generates random update operation sequences
    public static Arbitrary<Action<UpdateExpressionBuilder<TestEntity>>> UpdateOperations();
}
```

Generator constraints:
- Only reference properties that exist on `TestEntity` / `TestKeyedEntity`
- Combine comparison operators, logical operators, and DynamoDB functions randomly
- Include edge cases: empty strings, `Guid.Empty`, `DateTime.MinValue`, `null` nullable values, empty collections
- Exclude truly invalid expressions (method calls on non-string types, arithmetic) — those have dedicated negative tests

### Generator Complexity Tiers

| Tier      | Description                         | Example                                                  |
| --------- | ----------------------------------- | -------------------------------------------------------- |
| Simple    | Single property access / comparison | `x => x.Name == "foo"`                                   |
| Composite | 2-3 combined predicates             | `x => x.Name == "foo" && x.Count > 5`                    |
| Complex   | Nested properties + functions + NOT | `x => x.Address.City.StartsWith("L") && !(x.Count > 10)` |

## Invariants to Verify

### PR-01.1: Projection Builder Invariants

```csharp
[Property]
public Property ProjectionNeverProducesEmptyAliasForReservedKeyword(
    Expression<Func<TestEntity, object>> selector)
{
    // If any projected attribute is a reserved keyword,
    // the result must contain an alias for it
}

[Property]
public Property ProjectionExpressionIsValidCommaSeparatedList(
    Expression<Func<TestEntity, object>> selector)
{
    // Output is always empty or a comma-separated list of
    // valid attribute names / alias references / dotted paths
}

[Property]
public Property ProjectionAliasesAlwaysUseProjPrefix(
    Expression<Func<TestEntity, object>> selector)
{
    // Every key in ExpressionAttributeNames starts with #proj_
}
```

### PR-01.2: Filter/Condition Builder Invariants

```csharp
[Property]
public Property FilterNeverProducesEmptyExpression(
    Expression<Func<TestEntity, bool>> predicate)
{
    // Non-trivial predicate always produces non-empty expression string
}

[Property]
public Property FilterParenthesesAreBalanced(
    Expression<Func<TestEntity, bool>> predicate)
{
    // Count of '(' equals count of ')' in expression string
}

[Property]
public Property FilterValuePlaceholdersMatchDictionary(
    Expression<Func<TestEntity, bool>> predicate)
{
    // Every :filt_vN in expression string has a corresponding
    // entry in ExpressionAttributeValues, and vice versa
}

[Property]
public Property FilterNameAliasesMatchDictionary(
    Expression<Func<TestEntity, bool>> predicate)
{
    // Every #filt_N in expression string has a corresponding
    // entry in ExpressionAttributeNames, and vice versa
}

[Property]
public Property FilterAndConditionAliasesNeverCollide(
    Expression<Func<TestEntity, bool>> predicate)
{
    // Building same predicate as filter (#filt_) and condition (#cond_)
    // produces disjoint alias sets
}
```

### PR-01.3: Update Builder Invariants

```csharp
[Property]
public Property UpdateClausesAreWellFormed(
    Action<UpdateExpressionBuilder<TestEntity>> operations)
{
    // Output contains only valid clause keywords: SET, REMOVE, ADD, DELETE
    // Each clause keyword appears at most once
}

[Property]
public Property UpdateAliasesAlwaysUseUpdPrefix(
    Action<UpdateExpressionBuilder<TestEntity>> operations)
{
    // Every alias uses #upd_ / :upd_v prefix
}
```

### PR-01.4: Composability Invariants

```csharp
[Property]
public Property ComposedFiltersNeverHaveAliasCollisions(
    Expression<Func<TestEntity, bool>> left,
    Expression<Func<TestEntity, bool>> right)
{
    // After And() / Or(), no alias key appears in both
    // original left and re-aliased right dictionaries
}

[Property]
public Property ComposedFilterIsSemanticallySupersetOfBoth(
    Expression<Func<TestEntity, bool>> left,
    Expression<Func<TestEntity, bool>> right)
{
    // Composed expression string contains substrings
    // corresponding to both operands
}
```

### PR-01.5: Type Converter Invariants

```csharp
[Property]
public Property BuiltInConverterRoundTrips(object value)
{
    // For every supported .NET type:
    // converter.FromAttributeValue(converter.ToAttributeValue(value)) == value
}

[Property]
public Property NullableConverterPreservesNullSemantics(int? value)
{
    // null → NULL AttributeValue → null
    // non-null → N AttributeValue → original value
}
```

### PR-01.6: Key Condition Builder Invariants

```csharp
[Property]
public Property KeyConditionAlwaysContainsPartitionKeyEquality(
    string pkValue)
{
    // Every key condition expression contains exactly one equality
    // comparison for the partition key
}
```

## Configuration

```csharp
// In test assembly, configure FsCheck defaults
public class PropertyTestConfig
{
    public const int DefaultMaxTest = 1_000;        // default for dev and CI
}
```

- Default: 1,000 cases per property (balances speed vs coverage)
- Rapid iteration / agent workflows: `FSCHECK_MAX_TEST=100 dotnet test --filter "Category=Property"`
- Full validation runs: `FSCHECK_MAX_TEST=10000 dotnet test --filter "Category=Property"`
- Configurable via environment variable `FSCHECK_MAX_TEST`

## File Structure

```
DynamoDb.ExpressionMapping.Tests/
├── PropertyBased/
│   ├── Generators/
│   │   ├── ExpressionGenerators.cs
│   │   ├── ProjectionSelectorGenerator.cs
│   │   ├── FilterPredicateGenerator.cs
│   │   └── UpdateOperationGenerator.cs
│   ├── ProjectionBuilderProperties.cs
│   ├── FilterExpressionBuilderProperties.cs
│   ├── UpdateExpressionBuilderProperties.cs
│   ├── KeyConditionBuilderProperties.cs
│   ├── ComposabilityProperties.cs
│   └── TypeConverterProperties.cs
```

## Success Criteria

- All properties pass at default 1,000 cases; validated once at 10,000 via `FSCHECK_MAX_TEST=10000` before phase completion
- At least 3 real bugs discovered during initial implementation (validates the approach)
- Generators cover all three complexity tiers
- CI pipeline runs property tests as part of unit test suite
