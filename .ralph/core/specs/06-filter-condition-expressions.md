# Spec 06: Filter and Condition Expression Builder

## Motivation

DynamoDB `FilterExpression` and `ConditionExpression` are boolean expressions over item attributes. Building them as raw strings requires manual `ExpressionAttributeNames`/`ExpressionAttributeValues` management, is not refactorable, and lacks compile-time type checking. This builder converts C# lambda predicates into DynamoDB-compatible expression strings.

## Scope

This spec covers building **FilterExpression** and **ConditionExpression** from C# lambda predicates. These are functionally similar (both are boolean expressions over item attributes), differing only in where they are used:

- `FilterExpression` — applied after query/scan, before returning results
- `ConditionExpression` — applied on write operations (conditional puts/updates/deletes)

## Design

### 1. Interface

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB FilterExpression / ConditionExpression strings
/// from C# lambda predicates.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// </summary>
public interface IFilterExpressionBuilder<TSource>
{
    /// <summary>
    /// Builds a DynamoDB filter/condition expression from a predicate.
    /// </summary>
    FilterExpressionResult BuildFilter(
        Expression<Func<TSource, bool>> predicate);
}
```

### 2. FilterExpressionResult

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

public sealed class FilterExpressionResult
{
    /// <summary>
    /// The DynamoDB expression string.
    /// E.g. "#filt_0 = :filt_v0 AND #filt_1 = :filt_v1"
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Attribute name aliases for reserved keywords.
    /// E.g. { "#filt_0": "Status", "#filt_1": "Enabled" }
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>
    /// Attribute value placeholders.
    /// E.g. { ":filt_v0": { S: "Live" }, ":filt_v1": { BOOL: true } }
    /// </summary>
    public IReadOnlyDictionary<string, AttributeValue> ExpressionAttributeValues { get; }

    /// <summary>
    /// Whether this expression is empty (always-true predicate).
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(Expression);
}
```

### 3. Supported Predicate Patterns

#### Comparison operators
```csharp
p => p.Status == OrderStatus.Active
// → "#filt_0 = :filt_v0"

p => p.Total > 100m
// → "Total > :filt_v0"

p => p.CreatedAt >= someDate
// → "CreatedAt >= :filt_v0"
```

#### Boolean properties
```csharp
p => p.Enabled
// → "#filt_0 = :filt_v0"  (where :filt_v0 = { BOOL: true })

p => !p.Hidden
// → "#filt_0 = :filt_v0"  (where :filt_v0 = { BOOL: false })
```

#### Logical operators
```csharp
p => p.Enabled && p.Status == OrderStatus.Active
// → "(#filt_0 = :filt_v0) AND (#filt_1 = :filt_v1)"

p => p.Enabled || p.CustomerEnabled
// → "(#filt_0 = :filt_v0) OR (#filt_1 = :filt_v1)"

p => !(p.Status == OrderStatus.Expired)
// → "NOT (#filt_0 = :filt_v0)"
```

#### String operations
```csharp
p => p.Title.StartsWith("Premium")
// → "begins_with(Title, :filt_v0)"

p => p.Description.Contains("sale")
// → "contains(Description, :filt_v0)"
```

#### Null checks
```csharp
p => p.ExpiresOn == null
// → "attribute_not_exists(ExpiresOn)"

p => p.ExpiresOn != null
// → "attribute_exists(ExpiresOn)"
```

#### Attribute existence
```csharp
p => DynamoDbFunctions.AttributeExists(p.FallbackId)
// → "attribute_exists(FallbackId)"

p => DynamoDbFunctions.AttributeNotExists(p.FallbackId)
// → "attribute_not_exists(FallbackId)"
```

#### BETWEEN
```csharp
p => DynamoDbFunctions.Between(p.Price, 10, 50)
// → "Price BETWEEN :filt_v0 AND :filt_v1"
```

#### IN
```csharp
var statuses = new[] { OrderStatus.Active, OrderStatus.Pending };
p => statuses.Contains(p.Status)
// → "#filt_0 IN (:filt_v0, :filt_v1)"
```

#### Size function
```csharp
p => DynamoDbFunctions.Size(p.Tags) > 0
// → "size(Tags) > :filt_v0"
```

### 4. DynamoDbFunctions Static Class

Marker methods that the expression visitor recognises and translates to DynamoDB functions:

```csharp
namespace DynamoDb.ExpressionMapping;

/// <summary>
/// Static methods representing DynamoDB-specific functions.
/// These methods throw at runtime — they exist only as expression tree markers.
/// </summary>
public static class DynamoDbFunctions
{
    public static bool AttributeExists<T>(T property) =>
        throw new InvalidOperationException("Expression marker only");

    public static bool AttributeNotExists<T>(T property) =>
        throw new InvalidOperationException("Expression marker only");

    public static bool Between<T>(T property, T low, T high) where T : IComparable<T> =>
        throw new InvalidOperationException("Expression marker only");

    public static int Size<T>(T property) =>
        throw new InvalidOperationException("Expression marker only");

    public static bool AttributeType<T>(T property, string dynamoDbType) =>
        throw new InvalidOperationException("Expression marker only");
}
```

### 5. Alias Scoping

Filter aliases use `#filt_` prefix and `:filt_v` prefix for values to avoid collision with aliases from other builders:

```
Projections:  #proj_0, #proj_1, ...
Filters:      #filt_0, #filt_1, ... / :filt_v0, :filt_v1, ...
Conditions:   #cond_0, #cond_1, ... / :cond_v0, :cond_v1, ...
Updates:      #upd_0, #upd_1, ...   / :upd_v0, :upd_v1, ...
```

### 6. Composability

Filters can be combined programmatically via `And()` and `Or()` static methods on `FilterExpressionResult`.

#### 6.1 Alias Collision Problem

Each `BuildFilter()` call independently starts its alias counter at zero. Two independently built filters will both produce `#filt_0`, `:filt_v0`, etc. Naively merging their dictionaries would cause key collisions (the merge helpers in Spec 10 §6 rightfully throw `ExpressionAttributeConflictException` (Spec 14 §4) on conflicting keys).

#### 6.2 Re-aliasing Strategy

`And()` and `Or()` resolve this by **re-aliasing the right operand** before merging. The left operand is kept as-is; the right operand's aliases are shifted so that its indices start after the left operand's maximum index.

**Algorithm:**

1. Scan the left operand's `ExpressionAttributeNames` keys to find the maximum name index (`maxNameIdx`), and `ExpressionAttributeValues` keys to find the maximum value index (`maxValueIdx`). If the left operand is empty, both are `-1`.
2. For each alias in the right operand's `ExpressionAttributeNames`, rewrite `#filt_N` → `#filt_{N + maxNameIdx + 1}`. Apply the same rewrite to the right operand's expression string.
3. For each alias in the right operand's `ExpressionAttributeValues`, rewrite `:filt_vN` → `:filt_v{N + maxValueIdx + 1}`. Apply the same rewrite to the right operand's expression string.
4. Merge the rewritten right dictionaries into the left dictionaries using the standard merge helpers (which will no longer throw, since indices are now disjoint).
5. Combine the expression strings with the logical operator: `({left}) AND ({right})` or `({left}) OR ({right})`.

**Alias rewriting** is performed by regex-replacing `#filt_(\d+)` and `:filt_v(\d+)` in the expression string and rebuilding the dictionaries with shifted keys. Replacements must be applied in descending index order (highest index first) to prevent partial matches (e.g., `#filt_1` matching inside `#filt_10`).

#### 6.3 Edge Cases

| Scenario | Behaviour |
|---|---|
| Left is empty (`IsEmpty`) | Return right as-is (no wrapping, no re-aliasing) |
| Right is empty (`IsEmpty`) | Return left as-is |
| Both empty | Return empty result |
| Null operand | `ArgumentNullException` |
| Chained composition `And(And(a, b), c)` | Works naturally — the inner `And` produces a result with contiguous indices; the outer `And` re-aliases `c` starting after the inner result's max index |

#### 6.4 API

```csharp
public static FilterExpressionResult And(
    FilterExpressionResult left,
    FilterExpressionResult right)
{
    ArgumentNullException.ThrowIfNull(left);
    ArgumentNullException.ThrowIfNull(right);

    if (left.IsEmpty) return right;
    if (right.IsEmpty) return left;

    var (rewrittenExpr, rewrittenNames, rewrittenValues) =
        ReAlias(right, left);

    var mergedNames = new Dictionary<string, string>(left.ExpressionAttributeNames);
    RequestMergeHelpers.MergeAttributeNames(mergedNames, rewrittenNames);

    var mergedValues = new Dictionary<string, AttributeValue>(left.ExpressionAttributeValues);
    RequestMergeHelpers.MergeAttributeValues(mergedValues, rewrittenValues);

    return new FilterExpressionResult(
        expression: $"({left.Expression}) AND ({rewrittenExpr})",
        expressionAttributeNames: mergedNames,
        expressionAttributeValues: mergedValues);
}

public static FilterExpressionResult Or(
    FilterExpressionResult left,
    FilterExpressionResult right)
{
    // Identical to And() except the operator is OR.
}
```

#### 6.5 Re-aliasing Helper

```csharp
private static (string Expression,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyDictionary<string, AttributeValue> Values)
    ReAlias(FilterExpressionResult source, FilterExpressionResult reference)
{
    int nameOffset = MaxAliasIndex(reference.ExpressionAttributeNames.Keys, "#filt_") + 1;
    int valueOffset = MaxAliasIndex(reference.ExpressionAttributeValues.Keys, ":filt_v") + 1;

    var expr = source.Expression;
    var newNames = new Dictionary<string, string>();
    var newValues = new Dictionary<string, AttributeValue>();

    // Rewrite name aliases — process in descending index order
    foreach (var (oldKey, attr) in source.ExpressionAttributeNames
        .OrderByDescending(kvp => ExtractIndex(kvp.Key, "#filt_")))
    {
        int idx = ExtractIndex(oldKey, "#filt_");
        string newKey = $"#filt_{idx + nameOffset}";
        expr = expr.Replace(oldKey, newKey);
        newNames[newKey] = attr;
    }

    // Rewrite value aliases — process in descending index order
    foreach (var (oldKey, val) in source.ExpressionAttributeValues
        .OrderByDescending(kvp => ExtractIndex(kvp.Key, ":filt_v")))
    {
        int idx = ExtractIndex(oldKey, ":filt_v");
        string newKey = $":filt_v{idx + valueOffset}";
        expr = expr.Replace(oldKey, newKey);
        newValues[newKey] = val;
    }

    return (expr, newNames, newValues);
}

private static int MaxAliasIndex(
    IEnumerable<string> keys, string prefix)
{
    int max = -1;
    foreach (var key in keys)
    {
        int idx = ExtractIndex(key, prefix);
        if (idx > max) max = idx;
    }
    return max;
}

private static int ExtractIndex(string key, string prefix)
{
    // Returns the integer N from a key like "#filt_N" or ":filt_vN"
    return int.Parse(key[prefix.Length..]);
}
```

#### 6.6 Worked Example

```csharp
var filter1 = builder.BuildFilter(p => p.Status == "Active");
// Expression:  "#filt_0 = :filt_v0"
// Names:       { "#filt_0": "Status" }
// Values:      { ":filt_v0": { S: "Active" } }

var filter2 = builder.BuildFilter(p => p.Total > 100);
// Expression:  "Total > :filt_v0"
// Names:       { }
// Values:      { ":filt_v0": { N: "100" } }

var combined = FilterExpressionResult.And(filter1, filter2);
// Re-alias filter2:
//   nameOffset  = MaxIndex({"#filt_0"}) + 1 = 1  (no names to shift, but offset is still 1)
//   valueOffset = MaxIndex({":filt_v0"}) + 1 = 1
//   ":filt_v0" → ":filt_v1"
//   Expression becomes: "Total > :filt_v1"
//
// Merged result:
//   Expression: "(#filt_0 = :filt_v0) AND (Total > :filt_v1)"
//   Names:      { "#filt_0": "Status" }
//   Values:     { ":filt_v0": { S: "Active" }, ":filt_v1": { N: "100" } }

var filter3 = builder.BuildFilter(p => p.Enabled);
// Expression:  "#filt_0 = :filt_v0"
// Names:       { "#filt_0": "Enabled" }
// Values:      { ":filt_v0": { BOOL: true } }

var chained = FilterExpressionResult.And(combined, filter3);
// Re-alias filter3 against combined:
//   nameOffset  = MaxIndex({"#filt_0"}) + 1 = 1
//   valueOffset = MaxIndex({":filt_v0", ":filt_v1"}) + 1 = 2
//   "#filt_0"  → "#filt_1"
//   ":filt_v0" → ":filt_v2"
//   Expression becomes: "#filt_1 = :filt_v2"
//
// Final result:
//   Expression: "((#filt_0 = :filt_v0) AND (Total > :filt_v1)) AND (#filt_1 = :filt_v2)"
//   Names:      { "#filt_0": "Status", "#filt_1": "Enabled" }
//   Values:     { ":filt_v0": { S: "Active" }, ":filt_v1": { N: "100" },
//                 ":filt_v2": { BOOL: true } }
```

#### 6.7 Condition Expression Composability

`ConditionExpressionResult` provides identical `And()` / `Or()` static methods with the same re-aliasing logic, using the `#cond_` / `:cond_v` prefixes.

### 7. Captured Variables

The expression visitor must handle captured variables (closures):

```csharp
var minDate = DateTime.UtcNow;
var filter = builder.BuildFilter(p => p.CreatedAt > minDate);
// Must evaluate minDate at build time and embed as AttributeValue
```

This requires evaluating `ConstantExpression` and `MemberExpression` on closure types at expression analysis time.

### 8. Integration with Attribute Name Resolution and Value Conversion

The filter builder depends on three infrastructure components:

- **`IAttributeNameResolverFactory`** — resolves property names to DynamoDB attribute names (including nested paths across types, using `PropertyPath.SegmentProperties` — see Spec 02 §2)
- **`ExpressionValueEmitter`** (Spec 05 §11) — converts .NET constant/captured values to `AttributeValue` for the `ExpressionAttributeValues` dictionary, applying the full converter resolution order (Spec 05 §8): `[DynamoDbConverter]` attribute → registry exact match → Nullable → Enum → collection → throw

When the expression visitor encounters a comparison like `p => p.Status == OrderStatus.Active`, it:

1. Resolves the **left side** (property path) via the resolver factory to produce the attribute name
2. Resolves the **right side** (constant value) via `ExpressionValueEmitter.Emit(value, propertyInfo)`, which checks for `[DynamoDbConverter]` on the property and falls back to the registry

```csharp
// Flat property — resolved via factory.GetResolver(typeof(Order))
// [DynamoDbAttribute("cust_id")] on CustomerId
p => p.CustomerId == someGuid
// → "cust_id = :filt_v0"  (uses resolved attribute name)
// → :filt_v0 = valueEmitter.Emit(someGuid, customerIdPropertyInfo)  (Guid → { S: "..." })

// Nested property — each segment resolved against its declaring type
// via SegmentProperties (Spec 02 §2)
// [DynamoDbAttribute("city_name")] on Address.City
p => p.Address.City == "London"
// SegmentProperties[0].DeclaringType → typeof(Order), PropertyType → typeof(Address)
// SegmentProperties[1].DeclaringType → typeof(Address)
// → "Address.city_name = :filt_v0"
// → :filt_v0 = valueEmitter.Emit("London", cityPropertyInfo)

// Enum property — converter resolved via registry (Spec 05 §8 step 4)
p => p.Status == OrderStatus.Active
// → "#filt_0 = :filt_v0"
// → :filt_v0 = valueEmitter.Emit(Active, statusPropertyInfo) → { S: "Active" }
```

### 9. Validation

| Pattern | Behaviour |
|---|---|
| Unsupported method call | `UnsupportedExpressionException` with `NodeType` and `ExpressionText` (Spec 14 §2) |
| `[DynamoDbIgnore]` property in predicate | `InvalidFilterException` with `PropertyName` and `EntityType` in strict mode (Spec 14 §7) |
| Non-boolean expression | `InvalidFilterException` (Spec 14 §7) |
| Null predicate | `ArgumentNullException` |

### 10. Condition Expression Builder

`ConditionExpressionBuilder` is functionally identical to `FilterExpressionBuilder` but produces `ConditionExpressionResult` for use with `PutItemRequest.ConditionExpression`, `DeleteItemRequest.ConditionExpression`, etc. Implementation shares the same visitor with different alias prefixes.

```csharp
public interface IConditionExpressionBuilder<TSource>
{
    ConditionExpressionResult BuildCondition(
        Expression<Func<TSource, bool>> predicate);
}
```
