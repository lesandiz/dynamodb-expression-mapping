# Spec 03: Projection Expression Builder

## Motivation

DynamoDB `ProjectionExpression` controls which attributes are returned from read operations, reducing data transfer and improving performance. Building these strings manually is error-prone: reserved keyword aliasing, nested attribute dot-notation, and `ExpressionAttributeNames` management must all be correct. This builder automates that from C# lambda expressions.

## Design

### 1. Interface

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB ProjectionExpression strings from C# lambda expressions.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// </summary>
public interface IProjectionBuilder<TSource>
{
    /// <summary>
    /// Builds a DynamoDB projection from a LINQ selector expression.
    /// </summary>
    /// <typeparam name="TResult">The projected result type</typeparam>
    /// <param name="selector">Lambda expression defining which properties to project</param>
    /// <returns>Projection result containing expression string, attribute names, and metadata</returns>
    ProjectionResult BuildProjection<TResult>(
        Expression<Func<TSource, TResult>> selector);
}
```

### 2. ProjectionResult

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// The result of building a DynamoDB projection expression.
/// Immutable after construction.
/// </summary>
public sealed class ProjectionResult
{
    /// <summary>
    /// The DynamoDB ProjectionExpression string.
    /// E.g. "OrderId, #proj_0, Address.City"
    /// Empty string means "select all attributes" (no projection).
    /// </summary>
    public string ProjectionExpression { get; }

    /// <summary>
    /// ExpressionAttributeNames mapping for reserved keywords.
    /// E.g. {"#proj_0": "Status", "#proj_1": "Name"}
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>
    /// The property paths extracted from the expression, in order.
    /// </summary>
    public IReadOnlyList<PropertyPath> PropertyPaths { get; }

    /// <summary>
    /// The shape of the projection (Identity, SingleProperty, Composite).
    /// </summary>
    public ProjectionShape Shape { get; }

    /// <summary>
    /// Whether this projection is empty (selects all attributes).
    /// </summary>
    public bool IsEmpty => string.IsNullOrEmpty(ProjectionExpression);

    /// <summary>
    /// The DynamoDB attribute names that will be fetched (resolved names, not C# names).
    /// Useful for validation and logging.
    /// </summary>
    public IReadOnlyList<string> ResolvedAttributeNames { get; }
}
```

### 3. ProjectionBuilder Implementation

```csharp
namespace DynamoDb.ExpressionMapping.Expressions;

public sealed class ProjectionBuilder<TSource> : IProjectionBuilder<TSource>
{
    private readonly IAttributeNameResolverFactory resolverFactory;
    private readonly ReservedKeywordRegistry reservedKeywords;
    private readonly IExpressionCache cache;

    /// <summary>
    /// Creates a projection builder with a resolver factory for cross-type
    /// nested path resolution, and optional overrides.
    /// </summary>
    public ProjectionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        ReservedKeywordRegistry reservedKeywords = null,
        IExpressionCache cache = null)
    {
        this.resolverFactory = resolverFactory;
        this.reservedKeywords = reservedKeywords ?? ReservedKeywordRegistry.Default;
        this.cache = cache ?? ExpressionCache.Default;
    }
}
```

### 4. Build Algorithm

```
Input: Expression<Func<TSource, TResult>> selector

1. Check cache for this expression → return cached result if found

2. Extract property paths via ProjectionExpressionVisitor.ExtractPropertyPaths(selector)

3. If paths is empty → return ProjectionResult.Empty (identity/whole-object selection)

4. For each PropertyPath:
   a. Resolve each segment using the factory (see Section 5 for details):
      - Walk the path segments, obtaining the correct resolver per type
      - Segment 0: resolverFactory.GetResolver(typeof(TSource))
      - Segment N: resolverFactory.GetResolver(previousSegmentPropertyType)

   b. Validate: for each segment, check resolver.IsStoredAttribute(segmentName)
      - If false and strict mode → throw `InvalidProjectionException` (Spec 14 §6)
      - If false and lenient mode → skip this path

   c. Resolve: for each segment, attributeName = resolver.GetAttributeName(segmentName)
      - E.g. path ["Address", "City"] → "Address.City" (if names match)
      - E.g. path ["Address", "City"] → "addr.city_name" (if remapped on respective types)

   c. For each segment in the resolved attribute path:
      - Check reservedKeywords.IsReserved(segment)
      - Check ContainsSpecialCharacters(segment)
      - If either: generate alias, add to ExpressionAttributeNames

   d. Build the expression fragment:
      - Non-reserved: "Address.City"
      - Reserved segment: "#proj_0.City" (only the reserved segment aliased)
      - Both segments reserved: "#proj_0.#proj_1"

5. Join all fragments with ", "

6. Cache the result

7. Return ProjectionResult
```

### 5. Nested Attribute Handling (Cross-Type Resolution)

DynamoDB supports dot notation for nested attributes (Map type) in projections. The builder must preserve dots and resolve each segment against the correct type's resolver via `IAttributeNameResolverFactory`.

**Resolution walk for nested paths:**

The `PropertyPath.SegmentProperties` list (see Spec 02, Section 2) provides the `PropertyInfo` for every segment, eliminating additional reflection during resolution.

```
Expression: p => p.Address.City  (on Order)
SegmentProperties: [PropertyInfo(Order.Address), PropertyInfo(Address.City)]

Segment 0: "Address"
  → declaringType = SegmentProperties[0].DeclaringType  // typeof(Order)
  → resolver = resolverFactory.GetResolver(typeof(Order))
  → attributeName = resolver.GetAttributeName("Address")  // e.g. "addr"
  → nextType = SegmentProperties[0].PropertyType  // typeof(Address)

Segment 1: "City"
  → declaringType = SegmentProperties[1].DeclaringType  // typeof(Address)
  → resolver = resolverFactory.GetResolver(typeof(Address))
  → attributeName = resolver.GetAttributeName("City")  // e.g. "city_name"

Result: "addr.city_name"
```

**Examples:**

```csharp
// Expression: p => p.Address.City
// ProjectionExpression: "Address.City"  (convention — no remapping)

// Expression: p => new { p.Address.City, p.Address.PostCode }
// ProjectionExpression: "Address.City, Address.PostCode"

// With [DynamoDbAttribute("city_name")] on Address.City:
// Expression: p => p.Address.City
// ProjectionExpression: "Address.city_name"
```

Each segment of a dotted path is checked independently for reserved keywords:

```csharp
// If "Name" is reserved but "Contact" is not:
// p => p.Contact.Name
// → "Contact.#proj_0"
// ExpressionAttributeNames: { "#proj_0": "Name" }
```

### 6. Alias Generation

Aliases use a scoped prefix `#proj_` to avoid collision with aliases generated by filter, condition, or update builders:

```csharp
/// <summary>
/// Generates projection-scoped attribute name aliases.
/// Uses "#proj_N" prefix to avoid collision with filter/condition aliases.
/// </summary>
internal static class ProjectionAliasGenerator
{
    public static string Generate(uint index) => $"#proj_{index}";
}
```

### 7. Validation

| Condition | Behaviour |
|---|---|
| `selector` is null | `ArgumentNullException` |
| Property marked `[DynamoDbIgnore]` in strict mode | `InvalidProjectionException` with `PropertyName` and `EntityType` (Spec 14 §6) |
| Expression contains method calls | `UnsupportedExpressionException` with `NodeType` and `ExpressionText` (Spec 14 §2) |
| Empty property name after resolution | `InvalidOperationException` |

### 8. Thread Safety

- `ProjectionBuilder<TSource>` is thread-safe and designed for singleton/DI registration (one instance per entity type)
- `ProjectionResult` is immutable
- Cache is backed by `ConcurrentDictionary`

### 9. Example Outputs

```csharp
// Simple flat projection
p => new { p.OrderId, p.CustomerId }
// ProjectionExpression: "OrderId, CustomerId"
// ExpressionAttributeNames: {}

// With reserved keyword
p => new { p.OrderId, p.Status, p.Name }
// ProjectionExpression: "OrderId, #proj_0, #proj_1"
// ExpressionAttributeNames: { "#proj_0": "Status", "#proj_1": "Name" }

// With attribute remapping ([DynamoDbAttribute("cust_id")] on CustomerId)
p => new { p.OrderId, p.CustomerId }
// ProjectionExpression: "OrderId, cust_id"
// ExpressionAttributeNames: {}

// Nested attribute
p => new { p.Address.City, p.Address.PostCode }
// ProjectionExpression: "Address.City, Address.PostCode"
// ExpressionAttributeNames: {}

// Single property
p => p.OrderId
// ProjectionExpression: "OrderId"
// Shape: SingleProperty

// Identity (whole object)
p => p
// ProjectionExpression: "" (empty — all attributes)
// Shape: Identity
```
