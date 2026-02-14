# Spec 01: Attribute Name Mapping

## Motivation

DynamoDB attribute names often match C# property names by convention, but this is not always the case. A robust expression mapping library must support:

- Mapping a property `CustomerId` to a DynamoDB attribute `customer_id`
- Marking computed/derived properties that do not exist as stored DynamoDB attributes
- Validating at expression-build time that projected properties are actually stored
- Interoperating with AWS SDK annotations (`[DynamoDBProperty]`) already present on entity types

## Design

### 1. Attribute Annotations

```csharp
namespace DynamoDb.ExpressionMapping.Attributes;

/// <summary>
/// Maps a C# property to a DynamoDB attribute with a different name.
/// When absent, the property name is used as-is (convention-based default).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DynamoDbAttributeAttribute : Attribute
{
    public string AttributeName { get; }
    public DynamoDbAttributeAttribute(string attributeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(attributeName);
        AttributeName = attributeName;
    }
}

/// <summary>
/// Marks a property as not stored in DynamoDB.
/// Projecting this property will either:
/// - Throw at build time (strict mode, default)
/// - Be silently excluded (lenient mode)
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DynamoDbIgnoreAttribute : Attribute { }

/// <summary>
/// Specifies a custom converter for this property's AttributeValue serialisation.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DynamoDbConverterAttribute : Attribute
{
    public Type ConverterType { get; }
    public DynamoDbConverterAttribute(Type converterType)
    {
        ArgumentNullException.ThrowIfNull(converterType);
        ConverterType = converterType;
    }
}
```

### 2. IAttributeNameResolver

```csharp
namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Resolves C# property names to DynamoDB attribute names and vice versa.
/// </summary>
public interface IAttributeNameResolver
{
    /// <summary>
    /// Gets the DynamoDB attribute name for a C# property.
    /// </summary>
    /// <exception cref="InvalidProjectionException">
    /// Thrown when the property is marked with [DynamoDbIgnore] in strict mode (Spec 14).
    /// </exception>
    string GetAttributeName(string propertyName);

    /// <summary>
    /// Returns whether a property represents a stored DynamoDB attribute.
    /// False for computed properties marked with [DynamoDbIgnore].
    /// </summary>
    bool IsStoredAttribute(string propertyName);

    /// <summary>
    /// Gets the C# property name for a DynamoDB attribute name (reverse lookup).
    /// Used during result mapping.
    /// </summary>
    string GetPropertyName(string attributeName);
}

/// <summary>
/// Generic resolver that inspects type T for attribute annotations.
/// </summary>
public interface IAttributeNameResolver<T> : IAttributeNameResolver { }
```

### 3. AttributeNameResolver Implementation

```csharp
namespace DynamoDb.ExpressionMapping.Mapping;

public sealed class AttributeNameResolver<T> : IAttributeNameResolver<T>
{
    private readonly Dictionary<string, string> propertyToAttribute;  // C# name → DynamoDB name
    private readonly Dictionary<string, string> attributeToProperty;  // DynamoDB name → C# name
    private readonly HashSet<string> ignoredProperties;
    private readonly NameResolutionMode mode;

    public AttributeNameResolver(NameResolutionMode mode = NameResolutionMode.Strict)
    {
        this.mode = mode;
        // Reflect on T to build mappings from [DynamoDbAttribute] and [DynamoDbIgnore]
        // Cache per-type via static ConcurrentDictionary
    }
}
```

### 4. Resolution Modes

```csharp
public enum NameResolutionMode
{
    /// <summary>
    /// Throws when projecting a [DynamoDbIgnore] property. Default.
    /// </summary>
    Strict,

    /// <summary>
    /// Silently excludes [DynamoDbIgnore] properties from projections.
    /// The result mapper will populate them with default values.
    /// </summary>
    Lenient
}
```

### 5. Resolution Order

For a given property name, resolution follows this order:

1. Check explicit overrides registered via fluent API — highest priority
2. Check `[DynamoDbIgnore]` — if present, handle per mode
3. Check `[DynamoDbAttribute("name")]` (this library)
4. Check `[DynamoDBProperty("name")]` (AWS SDK)
5. Fall back to property name as-is (convention: property name = attribute name)

### 6. Fluent Configuration Override

For cases where the entity class cannot be modified (third-party types or shared models):

```csharp
var resolver = new AttributeNameResolverBuilder<MyEntity>()
    .Map(e => e.CustomerId, "customer_id")
    .Map(e => e.CreatedAt, "created_at")
    .Ignore(e => e.IsActive)
    .Ignore(e => e.ComputedStatus)
    .Build();
```

This takes precedence over attribute annotations, allowing per-context overrides without modifying the model.

### 7. AWS SDK Attribute Interop

The resolver should also recognise `[DynamoDBProperty("name")]` and `[DynamoDBIgnore]` from the `Amazon.DynamoDBv2.DataModel` namespace. This enables interop with entities already annotated for `DynamoDBContext` without requiring double-annotation.

Priority order when both are present:
1. Fluent overrides — highest priority
2. `[DynamoDbAttribute]` (this library)
3. `[DynamoDBProperty]` (AWS SDK)
4. Property name (default)

### 8. Caching

Type reflection is expensive. The resolver caches per-type metadata in a `static ConcurrentDictionary<Type, TypeMetadata>`. The cache is populated on first use and never invalidated (types do not change at runtime).

### 9. Validation at Build Time

When `ProjectionBuilder.BuildProjection()` is called with a resolver in strict mode:

- Properties marked `[DynamoDbIgnore]` in the selector throw `InvalidProjectionException` (Spec 14 §6)
- The exception carries `PropertyName` and `EntityType` as structured properties (inherited from `InvalidExpressionException`)
- This catches bugs at development time rather than producing silent incorrect results

### 10. IAttributeNameResolverFactory

A single `IAttributeNameResolver<T>` operates on one type. For nested paths like `p.Address.City`, the first segment (`Address`) belongs to the root type, but `City` belongs to the `Address` property's type. The factory provides resolvers for any type encountered during path resolution, enabling cross-type attribute name resolution.

```csharp
namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Provides IAttributeNameResolver instances for arbitrary types.
/// Used by expression builders to resolve attribute names across
/// type boundaries in nested property paths.
/// </summary>
public interface IAttributeNameResolverFactory
{
    /// <summary>
    /// Gets or creates a resolver for the specified entity type.
    /// Resolvers are cached after first creation.
    /// </summary>
    IAttributeNameResolver GetResolver(Type entityType);

    /// <summary>
    /// Gets or creates a typed resolver for <typeparamref name="T"/>.
    /// </summary>
    IAttributeNameResolver<T> GetResolver<T>();
}
```

### 11. AttributeNameResolverFactory Implementation

```csharp
namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Default factory that auto-creates resolvers via reflection.
/// For types with [DynamoDbAttribute], [DynamoDBProperty], or [DynamoDbIgnore]
/// annotations, resolvers are created automatically — no explicit registration needed.
/// Fluent overrides can be registered for types that require custom mappings.
/// </summary>
public sealed class AttributeNameResolverFactory : IAttributeNameResolverFactory
{
    private readonly ConcurrentDictionary<Type, IAttributeNameResolver> cache = new();
    private readonly NameResolutionMode mode;

    public AttributeNameResolverFactory(NameResolutionMode mode = NameResolutionMode.Strict)
    {
        this.mode = mode;
    }

    public IAttributeNameResolver GetResolver(Type entityType)
    {
        return cache.GetOrAdd(entityType, type =>
        {
            // Creates AttributeNameResolver<T> via reflection for the given type.
            // The resolver scans the type's properties for attribute annotations
            // using the same resolution order defined in Section 5.
            var resolverType = typeof(AttributeNameResolver<>).MakeGenericType(type);
            return (IAttributeNameResolver)Activator.CreateInstance(resolverType, mode)!;
        });
    }

    public IAttributeNameResolver<T> GetResolver<T>()
    {
        return (IAttributeNameResolver<T>)GetResolver(typeof(T));
    }

    /// <summary>
    /// Registers an explicit resolver for a type, overriding auto-discovery.
    /// Used for fluent-configured resolvers or test doubles.
    /// </summary>
    internal void Register(Type entityType, IAttributeNameResolver resolver)
    {
        cache[entityType] = resolver;
    }
}
```

### 12. Factory Builder for Fluent Overrides

When nested types require fluent configuration (types you cannot annotate), register them on the factory:

```csharp
var factory = new AttributeNameResolverFactoryBuilder()
    .WithMode(NameResolutionMode.Strict)
    .Configure<Order>(b => b.Map(o => o.CustomerId, "cust_id"))
    .Configure<Address>(b => b.Map(a => a.City, "city_name"))
    .Build();

// Auto-discovered types (no fluent overrides needed) are resolved on demand.
// Only types that deviate from convention/attribute-based mapping need explicit configuration.
```

For types not explicitly configured, the factory falls back to reflection-based auto-discovery — consumers only configure what deviates from convention.

### 13. Nested Path Resolution

Expression builders use the factory to resolve each segment of a nested path against the correct type. The `PropertyPath.SegmentProperties` list (see Spec 02, Section 2) provides the `PropertyInfo` for every segment, so no additional reflection is needed — each segment's declaring type and property type are already captured from the expression tree.

```
Path: ["Address", "City"] from expression p.Address.City on Order
SegmentProperties: [PropertyInfo(Order.Address), PropertyInfo(Address.City)]

1. Segment "Address"
   → declaringType = SegmentProperties[0].DeclaringType  // typeof(Order)
   → resolver = factory.GetResolver(typeof(Order))
   → attributeName = resolver.GetAttributeName("Address")  // e.g. "addr"
   → nextType = SegmentProperties[0].PropertyType  // typeof(Address)

2. Segment "City"
   → declaringType = SegmentProperties[1].DeclaringType  // typeof(Address)
   → resolver = factory.GetResolver(typeof(Address))
   → attributeName = resolver.GetAttributeName("City")  // e.g. "city_name"

Result: "addr.city_name" (or "Address.City" if no mappings)
```

Each segment's `IsStoredAttribute` check also uses the correct type's resolver.

### 14. PassThroughAttributeNameResolver

Default resolver when no attribute mapping is needed:

```csharp
/// <summary>
/// Default resolver: property name = attribute name, all properties are considered stored.
/// </summary>
internal sealed class PassThroughAttributeNameResolver : IAttributeNameResolver
{
    public static readonly PassThroughAttributeNameResolver Instance = new();

    public string GetAttributeName(string propertyName) => propertyName;
    public bool IsStoredAttribute(string propertyName) => true;
    public string GetPropertyName(string attributeName) => attributeName;
}
```

### 15. Example Usage

```csharp
public class OrderProjection
{
    public Guid OrderId { get; set; }

    [DynamoDbAttribute("cust_id")]
    public Guid CustomerId { get; set; }

    public decimal Total { get; set; }

    [DynamoDbIgnore]
    public bool IsHighValue => Total > 1000;  // Computed, not stored
}

var resolver = new AttributeNameResolver<OrderProjection>();

resolver.GetAttributeName("OrderId");      // → "OrderId"
resolver.GetAttributeName("CustomerId");   // → "cust_id"
resolver.IsStoredAttribute("IsHighValue"); // → false

// In projection builder:
// Expression: p => new { p.OrderId, p.CustomerId }
// ProjectionExpression: "OrderId, cust_id"
```
