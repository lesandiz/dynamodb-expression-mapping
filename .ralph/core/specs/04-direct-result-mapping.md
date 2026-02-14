# Spec 04: Direct Result Mapping

## Motivation

When using DynamoDB `ProjectionExpression` to fetch a subset of attributes, the response contains only the requested fields. The most efficient way to consume these results is to map the `Dictionary<string, AttributeValue>` directly to the target type `TResult`, constructing only the projected object — not hydrating a full entity and then discarding most of its properties.

A naive approach — deserialising all attributes into a full entity object and then applying the selector in memory — negates the bandwidth savings of projection by paying the CPU cost of constructing and populating properties that are immediately discarded. For entities with many properties or high-throughput streaming queries, this overhead is significant.

## Design

### 1. Core Interface

```csharp
namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Maps DynamoDB AttributeValue dictionaries directly to projected result types
/// without hydrating a full entity object.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// </summary>
public interface IDirectResultMapper<TSource>
{
    /// <summary>
    /// Creates a compiled, reusable mapper function for a given selector expression.
    /// The returned function maps directly from DynamoDB attributes to TResult.
    /// </summary>
    /// <typeparam name="TResult">The projected result type</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A compiled function that maps AttributeValue dictionaries to TResult</returns>
    Func<Dictionary<string, AttributeValue>, TResult> CreateMapper<TResult>(
        Expression<Func<TSource, TResult>> selector);

    /// <summary>
    /// One-shot mapping for single items. Uses cached mapper internally.
    /// </summary>
    TResult Map<TResult>(
        Dictionary<string, AttributeValue> attributes,
        Expression<Func<TSource, TResult>> selector);
}
```

### 2. Mapping Strategy by ProjectionShape

The mapper selects a strategy based on the `ProjectionShape` from the visitor:

#### Identity (p => p)
- Delegate to a full entity mapper (consumer-provided fallback)
- This is the escape hatch for when the consumer wants the complete entity

#### SingleProperty (p => p.OrderId)
- Read single attribute from dictionary
- Convert `AttributeValue` to target type
- Return directly (no intermediate object construction)

#### Composite (p => new { p.A, p.B } or p => new Dto { X = p.A })
- Read each required attribute from dictionary
- Convert each to the appropriate .NET type
- Construct the result object via expression compilation

### 3. Direct Mapping Algorithm

For a composite projection `p => new { p.OrderId, p.CustomerId, p.Enabled }`:

```
1. Analyse expression to extract:
   - PropertyPaths: [OrderId, CustomerId, Enabled]
   - Result type: anonymous type or named type
   - Constructor/initialiser shape

2. For each property path:
   a. Resolve DynamoDB attribute name via IAttributeNameResolver
   b. Determine .NET target type from PropertyInfo
   c. Select IAttributeValueConverter for that type
   d. Build an attribute reader: (dict) => converter.FromAttributeValue(dict["OrderId"])

3. Build a compiled delegate that:
   a. Reads each attribute from the dictionary
   b. Converts each to the .NET type
   c. Constructs TResult (via constructor args or property setters)

4. Cache the compiled delegate for reuse
```

### 4. Expression-Based Delegate Compilation

Instead of using reflection at runtime, build the mapping function as an expression tree and compile it once:

```csharp
// For: p => new { p.OrderId, p.CustomerId }
// Build equivalent of:
// (Dictionary<string, AttributeValue> attrs) =>
//     new <>f__AnonymousType<Guid, Guid>(
//         GuidConverter.Read(attrs, "OrderId"),
//         GuidConverter.Read(attrs, "CustomerId")
//     )

// Compiled to a Func<Dictionary<string, AttributeValue>, TResult>
```

This approach:
- Runs at native speed after initial compilation
- No per-invocation reflection
- No boxing for value types
- Handles both anonymous types (constructor) and named types (property setters)

### 5. Attribute Value Readers

Type-specific readers that extract and convert a single attribute:

```csharp
internal static class AttributeReaders
{
    // Each method handles null/missing attribute gracefully

    public static string ReadString(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL) return null;
        return av.S;
    }

    public static Guid ReadGuid(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL) return Guid.Empty;
        return Guid.TryParse(av.S, out var result) ? result : Guid.Empty;
    }

    public static bool ReadBool(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av)) return false;
        return av.BOOL;
    }

    public static int ReadInt(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL) return 0;
        return int.TryParse(av.N, out var result) ? result : 0;
    }

    public static DateTime ReadDateTime(Dictionary<string, AttributeValue> attrs, string key)
    {
        if (!attrs.TryGetValue(key, out var av) || av.NULL) return DateTime.MinValue;
        return DateTime.TryParse(av.S, null, DateTimeStyles.RoundtripKind, out var result)
            ? result : DateTime.MinValue;
    }

    // ... Nullable variants, decimal, double, long, List<string>, etc.
}
```

### 6. Nested Attribute Reading

For nested attributes (DynamoDB Map type `M`), navigation drills into the `M` dictionaries for intermediate segments, then dispatches to the appropriate typed reader for the leaf. The `PropertyPath.SegmentProperties` list (see Spec 02, Section 2) provides the `PropertyInfo` for every segment — intermediate segments are known to be Map types because their `PropertyType` is a complex object (not a scalar), and the leaf segment's `PropertyInfo` determines which typed reader or converter to use.

```csharp
// p => p.Address.City  (string leaf)
// DynamoDB: { "Address": { M: { "City": { S: "London" } } } }

// p => p.Address.Floor  (int leaf)
// DynamoDB: { "Address": { M: { "Floor": { N: "3" } } } }

/// <summary>
/// Navigates intermediate Map segments and returns the inner dictionary
/// containing the leaf attribute key. Returns null if any intermediate
/// segment is missing or not a Map.
/// </summary>
public static Dictionary<string, AttributeValue> NavigateToLeaf(
    Dictionary<string, AttributeValue> attrs, string[] path)
{
    var current = attrs;
    for (var i = 0; i < path.Length - 1; i++)
    {
        if (!current.TryGetValue(path[i], out var av) || av.M == null)
            return null;
        current = av.M;
    }
    return current;
}
```

The delegate compiler (§4) uses `NavigateToLeaf` to reach the leaf's container, then calls the typed reader (e.g. `ReadString`, `ReadInt`, `ReadGuid`) for the final segment based on the leaf property's .NET type:

```csharp
// Compiled delegate for p => p.Address.City (string):
// (attrs) => {
//     var leaf = NavigateToLeaf(attrs, ["Address", "City"]);
//     return leaf == null ? null : ReadString(leaf, "City");
// }

// Compiled delegate for p => p.Address.Floor (int):
// (attrs) => {
//     var leaf = NavigateToLeaf(attrs, ["Address", "Floor"]);
//     return leaf == null ? 0 : ReadInt(leaf, "Floor");
// }
```

For custom types with registered converters, the leaf reader falls back to the converter registry:

```csharp
// Compiled delegate for p => p.Address.GeoLocation (custom type with converter):
// (attrs) => {
//     var leaf = NavigateToLeaf(attrs, ["Address", "GeoLocation"]);
//     if (leaf == null || !leaf.TryGetValue("GeoLocation", out var av)) return default;
//     return (GeoLocation)converter.FromAttributeValue(av);
// }
```

### 7. Fallback Mode

For identity projections (`p => p`) or consumers who need full entity construction, support a fallback delegate:

```csharp
public sealed class DirectResultMapper<TSource> : IDirectResultMapper<TSource>
{
    private readonly IAttributeValueConverterRegistry converters;
    private readonly IAttributeNameResolver<TSource> resolver;
    private readonly ResultMapperCache cache;

    /// <summary>
    /// Optional fallback: if set, used for Identity projections (p => p)
    /// and as a safety net for types without registered converters.
    /// </summary>
    private readonly Func<Dictionary<string, AttributeValue>, object> fullEntityMapper;
}
```

### 8. Anonymous Type Support

Anonymous types have a constructor whose parameters match the properties in declaration order. The mapper must:

1. Detect anonymous types (via `CompilerGeneratedAttribute` or name pattern `<>f__AnonymousType`)
2. Get the constructor parameters
3. Match constructor parameters to expression arguments by position
4. Build a `NewExpression` with converted attribute readers as arguments

### 9. Named Type Support

Named types (DTOs, records) can be constructed via:

1. **Parameterless constructor + property setters** — `MemberInitExpression`
2. **Parameterised constructor** — `NewExpression` (common with records)
3. **Primary constructor** — treated same as parameterised constructor

The mapper handles all three by inspecting the original selector expression shape.

### 10. Error Handling

| Scenario | Behaviour |
|---|---|
| Attribute missing from dictionary | Return default value for type (null, 0, Guid.Empty, etc.) |
| Attribute present but wrong DynamoDB type | Attempt conversion, return default on failure, log warning |
| No converter registered for .NET type | `MissingConverterException` with `TargetType` and `PropertyName` at mapper creation time (Spec 14 §3) |
| Expression shape not supported | `UnsupportedExpressionException` with `NodeType` and `ExpressionText` at mapper creation time (Spec 14 §2) |

### 11. Example

```csharp
// Create a reusable mapper (directResultMapper is IDirectResultMapper<OrderProjection>)
var mapper = directResultMapper.CreateMapper<OrderSummary>(
    p => new OrderSummary { Id = p.OrderId, Total = p.Total });

// mapper is now a compiled Func<Dictionary<string, AttributeValue>, OrderSummary>
// Internally equivalent to:
// (attrs) => new OrderSummary
// {
//     Id = AttributeReaders.ReadGuid(attrs, "OrderId"),
//     Total = AttributeReaders.ReadDecimal(attrs, "Total")
// }

// Use in streaming query:
await foreach (var response in paginator.Responses)
{
    foreach (var item in response.Items)
    {
        yield return mapper(item); // Direct — no full entity hydration
    }
}
```
