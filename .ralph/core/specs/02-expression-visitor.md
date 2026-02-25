# Spec 02: Expression Tree Visitor

## Motivation

To convert C# lambda selectors like `p => new { p.Name, p.Age }` into DynamoDB `ProjectionExpression` strings, the library must extract which properties are accessed from the expression tree. This visitor is the foundational component that all expression builders depend on.

## Design

### 1. ProjectionExpressionVisitor

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Extracts property access paths from C# lambda expression trees.
/// Generic over TSource — not coupled to any specific entity type.
/// </summary>
public sealed class ProjectionExpressionVisitor : ExpressionVisitor
{
    public static IReadOnlyList<PropertyPath> ExtractPropertyPaths<TSource, TResult>(
        Expression<Func<TSource, TResult>> expression);
}
```

### 2. PropertyPath Value Object

```csharp
/// <summary>
/// Represents a property access path extracted from an expression tree.
/// </summary>
public sealed class PropertyPath
{
    /// <summary>
    /// The C# property name segments. E.g. ["Address", "City"] for p.Address.City
    /// </summary>
    public IReadOnlyList<string> Segments { get; }

    /// <summary>
    /// The full dotted path. E.g. "Address.City"
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// The leaf property name. E.g. "City"
    /// </summary>
    public string LeafName { get; }

    /// <summary>
    /// The PropertyInfo for every segment, parallel to <see cref="Segments"/>.
    /// E.g. for p.Address.City: [PropertyInfo(Address), PropertyInfo(City)].
    /// Each entry provides DeclaringType (for resolver lookup) and PropertyType
    /// (for determining the next segment's declaring type), eliminating the
    /// need for additional reflection during attribute name resolution.
    /// </summary>
    public IReadOnlyList<PropertyInfo> SegmentProperties { get; }

    /// <summary>
    /// Convenience accessor for the leaf property's PropertyInfo.
    /// Equivalent to SegmentProperties[^1]. Used for type-aware converter
    /// selection, [DynamoDbIgnore]/[DynamoDbAttribute] detection, and
    /// validation that the property is readable.
    /// </summary>
    public PropertyInfo PropertyInfo => SegmentProperties[^1];

    /// <summary>
    /// Whether this is a nested path (more than one segment).
    /// </summary>
    public bool IsNested => Segments.Count > 1;
}
```

### 3. Supported Expression Shapes

The visitor must handle these expression patterns:

#### a. Single property access
```csharp
p => p.OrderId
// Extracts: [PropertyPath("OrderId")]
```

#### b. Anonymous type (NewExpression)
```csharp
p => new { p.OrderId, p.CustomerId, p.Title }
// Extracts: [PropertyPath("OrderId"), PropertyPath("CustomerId"), PropertyPath("Title")]
```

#### c. Object initialiser (MemberInitExpression)
```csharp
p => new OrderSummary { Id = p.OrderId, Name = p.Title }
// Extracts: [PropertyPath("OrderId"), PropertyPath("Title")]
// Note: extracts SOURCE property names, not target property names
```

#### d. Nested property access
```csharp
p => p.Address.City
// Extracts: [PropertyPath(["Address", "City"])]
// DynamoDB projection: "Address.City" (dot notation for nested/map attributes)
```

#### e. Whole object selection (identity)
```csharp
p => p
// Extracts: [] (empty — means "all attributes")
```

#### f. Tuple / ValueTuple construction
```csharp
p => (p.OrderId, p.CustomerId)
// Extracts: [PropertyPath("OrderId"), PropertyPath("CustomerId")]
```

### 4. Method Call Expressions (Transparent Traversal)

Method calls in selector expressions (e.g. `Enum.Parse<T>(p.Property)`, `p.Property.ToString()`, `p.Name.Trim().ToUpper()`) are treated as **client-side transformations**. The visitor does not translate these to DynamoDB expressions — they execute during result mapping when the compiled selector runs against the deserialised entity.

`VisitMethodCall` recurses into:
- `node.Object` (for instance methods like `p.Name.ToUpper()`)
- `node.Arguments` (for static methods like `Enum.Parse<T>(p.Status)`)

This extracts any `MemberExpression` property references found within, without interpreting the method itself. The `_isLeafContext` flag propagates naturally from the parent `VisitNew`/`VisitMemberInit` call.

**Supported patterns:**

```csharp
// Instance method — extracts "Name"
p => p.Name.ToUpper()

// Static method — extracts "Status"
p => new { Status = Enum.Parse<OrderStatus>(p.Status) }

// Chained instance methods — extracts "Name"
p => p.Name.Trim().ToUpper()

// Nested: static wrapping instance — extracts "Status"
p => new { Status = Enum.Parse<OrderStatus>(p.Status.Trim()) }

// Multi-arg static method — extracts "Name" and "Title"
p => string.Equals(p.Name, p.Title)

// Mixed in composite — extracts "Name", "Status", "Price"
p => new { Upper = p.Name.Trim().ToUpper(), Status = Enum.Parse<OrderStatus>(p.Status), p.Price }
```

### 5. Unsupported Expressions (Must Throw)

The visitor must throw `UnsupportedExpressionException` (Spec 14 §2) for:

- **Arithmetic**: `p => p.Price * 1.1m`
- **String concatenation**: `p => p.First + " " + p.Last`
- **Conditional**: `p => p.IsActive ? p.StartDate : p.EndDate`
- **Array/collection indexing**: `p => p.Tags[0]`

The exception carries `NodeType` (`ExpressionType`) and `ExpressionText` (the `.ToString()` of the rejected node) as structured properties.

### 6. Deduplication

The visitor must not emit duplicate paths:

```csharp
p => new { p.OrderId, Same = p.OrderId }
// Extracts: [PropertyPath("OrderId")] (deduplicated)
```

### 7. Intermediate Node Filtering

For nested access like `p.Parent.Child`, the `VisitMember` method is called for both `p.Parent` and `p.Parent.Child` during tree traversal. The visitor must only emit **leaf** property paths — intermediate members in a chain must not be added as separate paths.

A member is a leaf when:
- It is directly used as a `NewExpression` argument
- It is directly used as a `MemberAssignment` value
- It is the body of the lambda itself

Implementation approach: track visitation context. Override `VisitNew` and `VisitMemberInit` to mark their arguments/bindings as leaf contexts before visiting them.

### 8. PropertyInfo Capture

For each extracted path, the visitor captures the `PropertyInfo` of **every segment** in the path, not just the leaf. The `MemberExpression.Member` cast already provides each `PropertyInfo` during tree traversal — the visitor simply collects them in order as it walks the chain from root to leaf.

For `p.Address.City`, the visitor produces:
```
SegmentProperties: [PropertyInfo(Order.Address), PropertyInfo(Address.City)]
```

This enables:
- **Cross-type attribute name resolution** — each segment's `PropertyInfo.DeclaringType` tells the resolver factory which type to resolve against, and `PropertyInfo.PropertyType` determines the declaring type for the next segment (see Spec 01, Section 13). No additional reflection is needed beyond what the expression tree already contains.
- **Type-aware converter selection** — the leaf `PropertyInfo` (accessible via `PropertyPath.PropertyInfo`) provides the .NET type for converter lookup during result mapping.
- **Attribute detection** — `[DynamoDbIgnore]`, `[DynamoDbAttribute]`, and `[DynamoDbConverter]` can be checked on any segment without re-reflecting.
- **Validation** — each segment can be verified as readable.

### 9. Expression Shape Detection

The visitor should expose what kind of result the expression produces:

```csharp
public enum ProjectionShape
{
    /// <summary>p => p (whole object)</summary>
    Identity,

    /// <summary>p => p.SingleProp (single value)</summary>
    SingleProperty,

    /// <summary>p => new { p.A, p.B } or p => new Dto { X = p.A }</summary>
    Composite
}
```

This is used downstream by the result mapper to choose the mapping strategy:
- `Identity` → no projection needed, full entity fetch
- `SingleProperty` → read one attribute, convert directly
- `Composite` → read multiple attributes, construct result object

### 10. Thread Safety

The visitor is instantiated per-call (not shared). The static `ExtractPropertyPaths` method creates a new instance internally. The result (`IReadOnlyList<PropertyPath>`) is immutable.

### 11. Performance Considerations

- Use `List<PropertyPath>` internally, return as `IReadOnlyList` (no `ImmutableList` allocation)
- `PropertyInfo` lookup via `MemberExpression.Member` cast (no additional reflection — the expression tree already contains it). For nested paths, intermediate `PropertyInfo` values are collected during chain traversal with no extra cost — the visitor already visits each `MemberExpression` node.
- Single pass over the expression tree (O(n) in tree size)
