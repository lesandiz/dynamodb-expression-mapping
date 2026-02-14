# Spec 13: Key Condition Expression Builder

## Motivation

Every DynamoDB `QueryRequest` requires a `KeyConditionExpression`. Unlike filter expressions (which accept arbitrary boolean predicates), key conditions are structurally constrained:

- Exactly one partition key equality condition is required
- An optional sort key condition supports a restricted operator set
- Only `AND` is allowed — `OR` and `NOT` are not permitted
- Only top-level key attributes are valid — no nested paths

Building key conditions as raw strings (as shown in Spec 10, Section 6) undermines the library's purpose of type-safe, attribute-resolved expression building. A dedicated fluent builder enforces these constraints at compile time rather than deferring to DynamoDB runtime errors.

## Scope

This spec covers building `KeyConditionExpression` strings via a fluent API that:

1. Enforces partition key equality as the required first step
2. Restricts sort key operators to the DynamoDB-supported set
3. Resolves attribute names and handles reserved keyword aliasing via the `#key_` / `:key_v` scope (Spec 08)
4. Produces a distinct `KeyConditionExpressionResult`

## Design

### 1. Fluent Builder Interface

The builder uses a staged fluent API. `WithPartitionKey` returns an intermediate type that either builds (partition-only query) or chains to a sort key condition.

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Entry point for building a DynamoDB KeyConditionExpression.
/// </summary>
public interface IKeyConditionExpressionBuilder<TSource>
{
    /// <summary>
    /// Begins the key condition with a partition key equality check.
    /// </summary>
    ISortKeyConditionBuilder<TSource> WithPartitionKey<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);
}
```

### 2. Sort Key Condition Builder

After specifying the partition key, the consumer may optionally add a sort key condition. Each sort key method terminates the chain and returns the result.

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Intermediate builder after partition key is specified.
/// Provides sort key condition methods or builds a partition-only expression.
/// </summary>
public interface ISortKeyConditionBuilder<TSource>
{
    /// <summary>Builds a partition-key-only expression (no sort key condition).</summary>
    KeyConditionExpressionResult Build();

    /// <summary>Sort key equals value.</summary>
    KeyConditionExpressionResult WithSortKeyEquals<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key less than value.</summary>
    KeyConditionExpressionResult WithSortKeyLessThan<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key less than or equal to value.</summary>
    KeyConditionExpressionResult WithSortKeyLessThanOrEqual<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key greater than value.</summary>
    KeyConditionExpressionResult WithSortKeyGreaterThan<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key greater than or equal to value.</summary>
    KeyConditionExpressionResult WithSortKeyGreaterThanOrEqual<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>Sort key between two values (inclusive).</summary>
    KeyConditionExpressionResult WithSortKeyBetween<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue low,
        TValue high);

    /// <summary>Sort key begins with a prefix (string/binary sort keys only).</summary>
    KeyConditionExpressionResult WithSortKeyBeginsWith(
        Expression<Func<TSource, string>> property,
        string prefix);
}
```

### 3. KeyConditionExpressionResult

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

public sealed class KeyConditionExpressionResult
{
    /// <summary>
    /// The DynamoDB KeyConditionExpression string.
    /// E.g. "#key_0 = :key_v0 AND #key_1 > :key_v1"
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Attribute name aliases for reserved keywords.
    /// E.g. { "#key_0": "PK", "#key_1": "SK" }
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>
    /// Attribute value placeholders.
    /// E.g. { ":key_v0": { S: "USER#123" }, ":key_v1": { S: "ORDER#2024" } }
    /// </summary>
    public IReadOnlyDictionary<string, AttributeValue> ExpressionAttributeValues { get; }

    internal KeyConditionExpressionResult(
        string expression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        IReadOnlyDictionary<string, AttributeValue> expressionAttributeValues)
    {
        Expression = expression;
        ExpressionAttributeNames = expressionAttributeNames;
        ExpressionAttributeValues = expressionAttributeValues;
    }
}
```

### 4. Usage Examples

#### Partition key only

```csharp
var keyCondition = new KeyConditionExpressionBuilder<Order>(resolverFactory, converters)
    .WithPartitionKey(o => o.PK, "USER#123")
    .Build();

// Expression:                "#key_0 = :key_v0"
// ExpressionAttributeNames:  { "#key_0": "PK" }
// ExpressionAttributeValues: { ":key_v0": { S: "USER#123" } }
```

#### Partition key + sort key equality

```csharp
var keyCondition = new KeyConditionExpressionBuilder<Order>(resolverFactory, converters)
    .WithPartitionKey(o => o.PK, "USER#123")
    .WithSortKeyEquals(o => o.SK, "ORDER#456");

// Expression: "#key_0 = :key_v0 AND #key_1 = :key_v1"
```

#### Partition key + sort key comparison

```csharp
var keyCondition = new KeyConditionExpressionBuilder<Order>(resolverFactory, converters)
    .WithPartitionKey(o => o.PK, "USER#123")
    .WithSortKeyGreaterThan(o => o.SK, "ORDER#2024-01-01");

// Expression: "#key_0 = :key_v0 AND #key_1 > :key_v1"
```

#### Partition key + sort key begins_with

```csharp
var keyCondition = new KeyConditionExpressionBuilder<Order>(resolverFactory, converters)
    .WithPartitionKey(o => o.PK, "USER#123")
    .WithSortKeyBeginsWith(o => o.SK, "ORDER#");

// Expression: "#key_0 = :key_v0 AND begins_with(#key_1, :key_v1)"
```

#### Partition key + sort key between

```csharp
var keyCondition = new KeyConditionExpressionBuilder<Order>(resolverFactory, converters)
    .WithPartitionKey(o => o.PK, "USER#123")
    .WithSortKeyBetween(o => o.SK, "ORDER#2024-01-01", "ORDER#2024-12-31");

// Expression: "#key_0 = :key_v0 AND #key_1 BETWEEN :key_v1 AND :key_v2"
```

#### Full fluent chaining with request extensions

```csharp
var request = new QueryRequest { TableName = tableName }
    .WithKeyCondition(keyConditionBuilder,
        b => b.WithPartitionKey(o => o.PK, "USER#123")
              .WithSortKeyBeginsWith(o => o.SK, "ORDER#"))
    .WithProjection(projectionBuilder,
        p => new OrderSummary { Id = p.OrderId, Total = p.Total })
    .WithFilter(filterBuilder,
        p => p.Status == OrderStatus.Active);
```

### 5. Property Resolution

Property expressions are resolved through `IAttributeNameResolverFactory`. Since key attributes are always top-level, only a single resolution step is needed (no nested paths).

```csharp
// [DynamoDbAttribute("pk")] on PK property
.WithPartitionKey(o => o.PK, "USER#123")
// Property "PK" → factory.GetResolver(typeof(Order)).GetAttributeName("PK")
// Resolves to "pk" → aliased as #key_0 if reserved or used directly
```

### 6. Value Conversion

Values are converted via `ExpressionValueEmitter` (Spec 05 §11), which applies the full converter resolution order (Spec 05 §8): `[DynamoDbConverter]` on the property → registry exact match → Nullable → Enum → open-generic collection → `MissingConverterException`.

```csharp
.WithPartitionKey(o => o.PK, "USER#123")
// valueEmitter.Emit("USER#123", pkPropertyInfo)
// → StringConverter (resolved via Spec 05 §8 step 2)
// → { S: "USER#123" }
// Stored as :key_v0
```

### 7. Alias Scoping

Key condition aliases use the `#key_` name prefix and `:key_v` value prefix (Spec 08, Section 3) to prevent collisions with projection, filter, condition, and update aliases on the same request:

```
Key Conditions: #key_0, #key_1 / :key_v0, :key_v1, :key_v2
```

### 8. Validation

| Condition | Behaviour |
|---|---|
| Null property expression | `ArgumentNullException` |
| `[DynamoDbIgnore]` property | `InvalidKeyConditionException` with `PropertyName` and `EntityType` (Spec 14 §9) |
| Nested property path (e.g. `o => o.Address.City`) | `InvalidKeyConditionException` with `PropertyName` — key attributes must be top-level (Spec 14 §9) |
| Null value for partition key | `ArgumentNullException` |
| `between` with low > high | `ArgumentException` |
| `begins_with` with null/empty prefix | `ArgumentException` |

### 9. Expression Construction

The builder constructs the expression string as follows:

1. **Partition key only**: `"#key_0 = :key_v0"`
2. **With sort key comparison**: `"#key_0 = :key_v0 AND #key_1 {op} :key_v1"` where `{op}` is `=`, `<`, `<=`, `>`, `>=`
3. **With sort key between**: `"#key_0 = :key_v0 AND #key_1 BETWEEN :key_v1 AND :key_v2"`
4. **With sort key begins_with**: `"#key_0 = :key_v0 AND begins_with(#key_1, :key_v1)"`

Smart aliasing applies: if the attribute name is not a reserved keyword and contains no special characters, it is used directly without aliasing (Spec 08, Section 6).

### 10. Implementation Class

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

public sealed class KeyConditionExpressionBuilder<TSource>
    : IKeyConditionExpressionBuilder<TSource>
{
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly IAttributeValueConverterRegistry converters;

    public KeyConditionExpressionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converters)
    {
        this.resolverFactory = resolverFactory ??
            throw new ArgumentNullException(nameof(resolverFactory));
        this.converters = converters ??
            throw new ArgumentNullException(nameof(converters));
    }

    public ISortKeyConditionBuilder<TSource> WithPartitionKey<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value)
    {
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(value);

        // 1. Extract property name from expression, validate top-level
        // 2. Resolve attribute name via factory
        // 3. Convert value via converter registry
        // 4. Generate #key_0 / :key_v0 aliases
        // 5. Return SortKeyConditionBuilder with partition key state

        return new SortKeyConditionBuilder<TSource>(
            resolverFactory, converters, partitionKeyState);
    }
}
```

### 11. Thread Safety

`KeyConditionExpressionBuilder<TSource>` is stateless — all state is captured in the `SortKeyConditionBuilder` returned by `WithPartitionKey`. Multiple threads can share a single builder instance and call `WithPartitionKey` concurrently, as each call produces an independent `ISortKeyConditionBuilder<TSource>`.
