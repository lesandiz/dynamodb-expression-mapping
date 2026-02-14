# Spec 07: Update Expression Builder

## Motivation

DynamoDB update expressions have four clauses (`SET`, `REMOVE`, `ADD`, `DELETE`) and require manual string construction with `ExpressionAttributeNames` and `ExpressionAttributeValues`. A typed fluent API provides compile-time safety and eliminates string manipulation errors.

Expression trees are not a natural fit for updates (they describe reads, not writes), so this uses a fluent builder API with lambda expressions for property selection only.

## Design

### 1. Interface

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB UpdateExpression strings from a fluent builder API.
/// </summary>
public interface IUpdateExpressionBuilder<TSource>
{
    /// <summary>SET attr = value</summary>
    IUpdateExpressionBuilder<TSource> Set<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>SET attr = attr + value (increment)</summary>
    IUpdateExpressionBuilder<TSource> Increment<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue amount) where TValue : struct;

    /// <summary>SET attr = attr - value (decrement)</summary>
    IUpdateExpressionBuilder<TSource> Decrement<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue amount) where TValue : struct;

    /// <summary>SET attr = if_not_exists(attr, value)</summary>
    IUpdateExpressionBuilder<TSource> SetIfNotExists<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>SET attr = list_append(attr, value)</summary>
    IUpdateExpressionBuilder<TSource> AppendToList<TValue>(
        Expression<Func<TSource, List<TValue>>> property,
        List<TValue> values);

    /// <summary>REMOVE attr</summary>
    IUpdateExpressionBuilder<TSource> Remove<TValue>(
        Expression<Func<TSource, TValue>> property);

    /// <summary>ADD value to number or set</summary>
    IUpdateExpressionBuilder<TSource> Add<TValue>(
        Expression<Func<TSource, TValue>> property,
        TValue value);

    /// <summary>DELETE value from set</summary>
    IUpdateExpressionBuilder<TSource> Delete<TValue>(
        Expression<Func<TSource, HashSet<TValue>>> property,
        HashSet<TValue> values);

    /// <summary>Build the final expression result.</summary>
    UpdateExpressionResult Build();
}
```

### 2. UpdateExpressionResult

```csharp
public sealed class UpdateExpressionResult
{
    /// <summary>
    /// The DynamoDB UpdateExpression string.
    /// E.g. "SET #upd_0 = :upd_v0, #upd_1 = #upd_1 + :upd_v1 REMOVE #upd_2"
    /// </summary>
    public string Expression { get; }

    /// <summary>Attribute name aliases.</summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>Attribute value placeholders.</summary>
    public IReadOnlyDictionary<string, AttributeValue> ExpressionAttributeValues { get; }

    /// <summary>Whether no operations were added.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Expression);
}
```

### 3. Usage Example

```csharp
var update = new UpdateExpressionBuilder<OrderProjection>(resolverFactory, converters)
    .Set(p => p.Status, OrderStatus.Active)
    .Increment(p => p.ViewCount, 1)
    .SetIfNotExists(p => p.CreatedAt, DateTime.UtcNow)
    .Remove(p => p.TempFlag)
    .Build();

// Expression: "SET #upd_0 = :upd_v0, #upd_1 = #upd_1 + :upd_v1,
//              #upd_2 = if_not_exists(#upd_2, :upd_v2) REMOVE #upd_3"
```

### 4. Expression Construction

Each clause type collects operations, then `Build()` concatenates them:

```
SET clause1, clause2, clause3 REMOVE clause4 ADD clause5 DELETE clause6
```

- Multiple operations within a clause are comma-separated
- Clauses appear only if they have operations

### 5. Property Resolution

Property expressions are resolved through `IAttributeNameResolverFactory`, which provides the correct resolver for each type encountered in nested paths. For flat properties, the factory resolves via `typeof(TSource)`. For nested paths, each segment is resolved against its declaring type (see Spec 01, Section 13).

```csharp
// Flat property — resolved via factory.GetResolver(typeof(Order))
.Set(p => p.CustomerId, someGuid)
// Property "CustomerId" → resolved to "cust_id" if mapped
// Aliased if reserved: #upd_0 → "cust_id"

// Nested property — each segment resolved against its declaring type
.Set(p => p.Address.City, "London")
// Segment "Address" → factory.GetResolver(typeof(Order)).GetAttributeName("Address")
// Segment "City"    → factory.GetResolver(typeof(Address)).GetAttributeName("City")
// Result: "Address.city_name" (if City is mapped to "city_name" on Address)
```

### 6. Value Conversion

Values are converted via `ExpressionValueEmitter` (Spec 05 §11), which applies the full converter resolution order (Spec 05 §8): `[DynamoDbConverter]` on the property → registry exact match → Nullable → Enum → open-generic collection → `MissingConverterException`.

```csharp
.Set(p => p.Status, OrderStatus.Active)
// valueEmitter.Emit(Active, statusPropertyInfo)
// → EnumConverter<OrderStatus> (resolved via Spec 05 §8 step 4)
// → { S: "Active" }
// Stored as :upd_v0

.Set(p => p.Total, new Money(99.99m, "USD"))
// valueEmitter.Emit(money, totalPropertyInfo)
// → MoneyConverter (resolved via [DynamoDbConverter] on Total — Spec 05 §8 step 1)
// → { M: { "Amount": { N: "99.99" }, "Currency": { S: "USD" } } }
// Stored as :upd_v1
```

### 7. Validation

| Condition | Behaviour |
|---|---|
| Null property expression | `ArgumentNullException` |
| `[DynamoDbIgnore]` property | `InvalidUpdateException` with `PropertyName` and `EntityType` (Spec 14 §8) |
| No operations added before `Build()` | Returns `UpdateExpressionResult.Empty` |
| Duplicate property in same clause | Last operation wins (overwrites previous) |
| Conflicting clauses (SET + REMOVE same property) | `InvalidUpdateException` with `PropertyName` (Spec 14 §8) |
