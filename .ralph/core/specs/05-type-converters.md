# Spec 05: Type Converter System

## Motivation

DynamoDB stores data in `AttributeValue` objects with type-specific fields (`S` for strings, `N` for numbers, `BOOL` for booleans, `M` for maps, `L` for lists, etc.). Converting between .NET types and `AttributeValue` is a fundamental operation that must be:

- **Extensible** — consumers can register converters for custom types
- **Reusable** — the same converter works across all entities, not embedded in entity-specific mappers
- **Performant** — no boxing for value types, O(1) lookup
- **Consistent** — identical serialisation format for the same .NET type regardless of which entity it appears on

## Design

### 1. Core Interface

```csharp
namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Converts between .NET types and DynamoDB AttributeValue.
/// Implementations are stateless and thread-safe.
/// </summary>
public interface IAttributeValueConverter
{
    /// <summary>
    /// The .NET type this converter handles.
    /// </summary>
    Type TargetType { get; }

    /// <summary>
    /// Converts a .NET value to a DynamoDB AttributeValue.
    /// </summary>
    AttributeValue ToAttributeValue(object value);

    /// <summary>
    /// Converts a DynamoDB AttributeValue to a .NET value.
    /// </summary>
    object FromAttributeValue(AttributeValue attributeValue);
}

/// <summary>
/// Strongly-typed converter interface for compile-time safety
/// and avoiding boxing for value types.
/// </summary>
public interface IAttributeValueConverter<T> : IAttributeValueConverter
{
    new AttributeValue ToAttributeValue(T value);
    new T FromAttributeValue(AttributeValue attributeValue);
}
```

### 2. Converter Registry

```csharp
namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Registry of type converters. Thread-safe, supports runtime registration.
/// </summary>
public interface IAttributeValueConverterRegistry
{
    /// <summary>
    /// Gets the converter for a .NET type.
    /// </summary>
    /// <exception cref="MissingConverterException">No converter registered for the type (Spec 14 §3).</exception>
    IAttributeValueConverter GetConverter(Type type);

    /// <summary>
    /// Gets a strongly-typed converter.
    /// </summary>
    IAttributeValueConverter<T> GetConverter<T>();

    /// <summary>
    /// Checks if a converter is registered for the type.
    /// </summary>
    bool HasConverter(Type type);

    /// <summary>
    /// Registers a custom converter. Overwrites any existing converter for the same type.
    /// </summary>
    void Register<T>(IAttributeValueConverter<T> converter);
}

public sealed class AttributeValueConverterRegistry : IAttributeValueConverterRegistry
{
    /// <summary>
    /// Default registry with all built-in converters pre-registered.
    /// This instance is frozen — calling <see cref="Register{T}"/> on it
    /// throws <see cref="InvalidOperationException"/>.
    /// Use <see cref="Clone"/> to obtain a mutable copy for customisation.
    /// </summary>
    public static readonly AttributeValueConverterRegistry Default = CreateDefault();

    private readonly ConcurrentDictionary<Type, IAttributeValueConverter> converters;
    private readonly bool frozen;

    /// <summary>
    /// Registers a custom converter. Overwrites any existing converter for the same type.
    /// Throws <see cref="InvalidOperationException"/> if this registry is frozen
    /// (i.e. the <see cref="Default"/> singleton).
    /// </summary>
    public void Register<T>(IAttributeValueConverter<T> converter)
    {
        if (frozen)
            throw new InvalidOperationException(
                "Cannot mutate the default converter registry. " +
                "Use Clone() to create a mutable copy, or register converters " +
                "via DynamoDbExpressionConfig.Builder.WithConverter().");

        converters[typeof(T)] = converter;
    }

    /// <summary>
    /// Creates a deep copy of this registry, including all registered converters.
    /// The clone is mutable regardless of whether the source is frozen.
    /// Used by <see cref="DynamoDbExpressionConfig.Builder"/> to customise converters
    /// without mutating the shared <see cref="Default"/> instance (Spec 11 §2).
    /// </summary>
    public AttributeValueConverterRegistry Clone()
    {
        var clone = new AttributeValueConverterRegistry(frozen: false);
        foreach (var kvp in this.converters)
            clone.converters[kvp.Key] = kvp.Value;
        return clone;
    }

    private static AttributeValueConverterRegistry CreateDefault()
    {
        var registry = new AttributeValueConverterRegistry(frozen: false);
        registry.RegisterBuiltIns();
        // Return a frozen copy so the singleton cannot be mutated.
        var frozen = new AttributeValueConverterRegistry(frozen: true);
        foreach (var kvp in registry.converters)
            frozen.converters[kvp.Key] = kvp.Value;
        return frozen;
    }
}
```

### 3. Built-in Converters

| .NET Type | DynamoDB Type | Read Behaviour | Write Behaviour | Null/Missing Handling |
|---|---|---|---|---|
| `string` | `S` | `av.S` | `new AV { S = value }` | `null` ↔ `NULL = true` or missing |
| `Guid` | `S` | `Guid.Parse(av.S)` | `new AV { S = value.ToString() }` | `Guid.Empty` if missing |
| `bool` | `BOOL` | `av.BOOL` | `new AV { BOOL = value }` | `false` if missing |
| `int` | `N` | `int.Parse(av.N)` | `new AV { N = value.ToString() }` | `0` if missing |
| `long` | `N` | `long.Parse(av.N)` | `new AV { N = value.ToString() }` | `0` if missing |
| `decimal` | `N` | `decimal.Parse(av.N)` | `new AV { N = value.ToString() }` | `0` if missing |
| `double` | `N` | `double.Parse(av.N)` | `new AV { N = value.ToString() }` | `0` if missing |
| `DateTime` | `S` (ISO 8601) | `DateTime.Parse(av.S, RoundtripKind)` | `new AV { S = value.ToString("O") }` | `DateTime.MinValue` if missing |
| `DateTimeOffset` | `S` (ISO 8601) | `DateTimeOffset.Parse(av.S, RoundtripKind)` | `new AV { S = value.ToString("O") }` | `DateTimeOffset.MinValue` if missing |
| `byte[]` | `B` | `av.B.ToArray()` | `new AV { B = new MemoryStream(value) }` | `null` if missing |
| `List<string>` | `L` | `av.L.Select(x => x.S)` | `new AV { L = items.Select(s => new AV{S=s}) }` | Empty list if missing |
| `List<int>` | `L` | `av.L.Select(x => int.Parse(x.N))` | Similar | Empty list if missing |
| `HashSet<string>` | `SS` | `new HashSet(av.SS)` | `new AV { SS = value.ToList() }` | Empty set if missing |
| `Dictionary<string,string>` | `M` | Map conversion | Map conversion | Empty dict if missing |
| `Enum` (any) | `S` or `N` | `Enum.Parse<T>(av.S)` | `new AV { S = value.ToString() }` | Default enum value |

### 4. Nullable Wrapper Converter

Rather than duplicating every converter for `Nullable<T>`, use a generic wrapper:

```csharp
internal sealed class NullableConverter<T> : IAttributeValueConverter<T?>
    where T : struct
{
    private readonly IAttributeValueConverter<T> inner;

    public T? FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL)
            return null;
        return inner.FromAttributeValue(attributeValue);
    }

    public AttributeValue ToAttributeValue(T? value)
    {
        if (!value.HasValue)
            return new AttributeValue { NULL = true };
        return inner.ToAttributeValue(value.Value);
    }
}
```

The registry automatically wraps `IAttributeValueConverter<T>` in `NullableConverter<T>` when `Nullable<T>` is requested.

### 5. Enum Converter

Generic converter for all enum types:

```csharp
internal sealed class EnumConverter<TEnum> : IAttributeValueConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly EnumStorageMode mode;

    public TEnum FromAttributeValue(AttributeValue attributeValue)
    {
        return mode switch
        {
            EnumStorageMode.String => Enum.Parse<TEnum>(attributeValue.S, ignoreCase: true),
            EnumStorageMode.Number => (TEnum)(object)int.Parse(attributeValue.N),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}

public enum EnumStorageMode
{
    /// <summary>Stored as string name (default)</summary>
    String,
    /// <summary>Stored as numeric value</summary>
    Number
}
```

### 6. Custom Converter Registration

Consumers register custom converters for domain types:

```csharp
public record Money(decimal Amount, string Currency);

public class MoneyConverter : IAttributeValueConverter<Money>
{
    public Money FromAttributeValue(AttributeValue av)
    {
        var map = av.M;
        return new Money(
            decimal.Parse(map["Amount"].N),
            map["Currency"].S);
    }

    public AttributeValue ToAttributeValue(Money value)
    {
        return new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["Amount"] = new() { N = value.Amount.ToString() },
                ["Currency"] = new() { S = value.Currency }
            }
        };
    }
}

// Registration
registry.Register(new MoneyConverter());
```

### 7. Per-Property Converter Override

Via `[DynamoDbConverter]` attribute:

```csharp
public class Order
{
    [DynamoDbConverter(typeof(MoneyConverter))]
    public Money Total { get; set; }

    public DateTime CreatedAt { get; set; }  // Uses default DateTime converter
}
```

The direct result mapper checks for `[DynamoDbConverter]` on each property before falling back to the registry.

### 8. Converter Resolution Order

1. `[DynamoDbConverter(typeof(...))]` on the property
2. Explicit registration in the registry for the exact type
3. `NullableConverter<T>` wrapper if `Nullable<T>` and converter exists for `T`
4. `EnumConverter<T>` if type is an enum
5. Open-generic collection resolution (see Section 8a)
6. Throw `MissingConverterException` (Spec 14 §3) with `TargetType` set to the requested type

### 8a. Open-Generic Collection Resolution

When no exact-type converter is registered (steps 1–4 miss), the registry checks whether the requested type matches a known generic collection pattern. If the element/value type has a resolvable converter (recursively applying this same resolution order), the registry composes a collection converter automatically.

**Resolution rules:**

```csharp
// If requested type is List<T> or IReadOnlyList<T>:
if (type.IsGenericType && (
    type.GetGenericTypeDefinition() == typeof(List<>) ||
    type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
{
    var elementConverter = GetConverter(elementType);  // recursive
    return new ListConverter<T>(elementConverter);
}

// If requested type is HashSet<T> or ISet<T>:
if (type.IsGenericType && (
    type.GetGenericTypeDefinition() == typeof(HashSet<>) ||
    type.GetGenericTypeDefinition() == typeof(ISet<>)))
{
    var elementConverter = GetConverter(elementType);  // recursive
    return new SetConverter<T>(elementConverter);
}

// If requested type is Dictionary<string, TValue> or IReadOnlyDictionary<string, TValue>:
if (type.IsGenericType && (
    type.GetGenericTypeDefinition() == typeof(Dictionary<,>) ||
    type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
    && type.GetGenericArguments()[0] == typeof(string))
{
    var valueConverter = GetConverter(valueType);  // recursive
    return new MapConverter<TValue>(valueConverter);
}
```

**Generic collection converters:**

```csharp
internal sealed class ListConverter<T> : IAttributeValueConverter<List<T>>
{
    private readonly IAttributeValueConverter<T> elementConverter;

    public List<T> FromAttributeValue(AttributeValue av)
    {
        if (av?.L == null) return new List<T>();
        return av.L.Select(e => elementConverter.FromAttributeValue(e)).ToList();
    }

    public AttributeValue ToAttributeValue(List<T> value)
    {
        return new AttributeValue
        {
            L = value.Select(e => elementConverter.ToAttributeValue(e)).ToList()
        };
    }
}

internal sealed class SetConverter<T> : IAttributeValueConverter<HashSet<T>>
{
    private readonly IAttributeValueConverter<T> elementConverter;

    public HashSet<T> FromAttributeValue(AttributeValue av)
    {
        // Use native SS/NS when the element converter produces S or N,
        // otherwise fall back to L (generic list).
        if (av?.SS != null && av.SS.Count > 0)
            return av.SS.Select(s => elementConverter.FromAttributeValue(
                new AttributeValue { S = s })).ToHashSet();
        if (av?.NS != null && av.NS.Count > 0)
            return av.NS.Select(n => elementConverter.FromAttributeValue(
                new AttributeValue { N = n })).ToHashSet();
        if (av?.L != null)
            return av.L.Select(e => elementConverter.FromAttributeValue(e)).ToHashSet();
        return new HashSet<T>();
    }

    public AttributeValue ToAttributeValue(HashSet<T> value)
    {
        // Probe the element converter's output to determine native set format.
        // If elements serialise to S → use SS; if N → use NS; otherwise → L.
        var sample = elementConverter.ToAttributeValue(value.First());
        if (sample.S != null)
            return new AttributeValue { SS = value.Select(e =>
                elementConverter.ToAttributeValue(e).S).ToList() };
        if (sample.N != null)
            return new AttributeValue { NS = value.Select(e =>
                elementConverter.ToAttributeValue(e).N).ToList() };
        return new AttributeValue { L = value.Select(e =>
            elementConverter.ToAttributeValue(e)).ToList() };
    }
}

internal sealed class MapConverter<TValue> : IAttributeValueConverter<Dictionary<string, TValue>>
{
    private readonly IAttributeValueConverter<TValue> valueConverter;

    public Dictionary<string, TValue> FromAttributeValue(AttributeValue av)
    {
        if (av?.M == null) return new Dictionary<string, TValue>();
        return av.M.ToDictionary(
            kvp => kvp.Key,
            kvp => valueConverter.FromAttributeValue(kvp.Value));
    }

    public AttributeValue ToAttributeValue(Dictionary<string, TValue> value)
    {
        return new AttributeValue
        {
            M = value.ToDictionary(
                kvp => kvp.Key,
                kvp => valueConverter.ToAttributeValue(kvp.Value))
        };
    }
}
```

**Design notes:**

- **Precedence**: Built-in exact-type converters for `List<string>`, `List<int>`, `HashSet<string>`, and `Dictionary<string,string>` (step 2) take precedence over generic resolution (step 5). No behavioural change for pre-registered types.
- **Recursive composition**: `List<List<string>>` resolves to `ListConverter<List<string>>` wrapping `ListConverter<string>`. DynamoDB supports nested lists, so this is valid.
- **Caching**: Composed converter instances are cached in the registry after first resolution, so step 5 runs at most once per type.
- **Map key constraint**: DynamoDB Map keys are always strings. `Dictionary<int, string>` would fall through to step 6 (`MissingConverterException`) because the key type is not `string`.

### 9. Performance

- Converters are stateless singletons — no allocation per conversion
- Generic `IAttributeValueConverter<T>` avoids boxing for value types (`int`, `bool`, `Guid`, `DateTime`)
- Registry lookup is O(1) via `ConcurrentDictionary<Type, IAttributeValueConverter>`
- The direct result mapper resolves converters once at mapper creation time, not per-item

### 10. Null Handling Mode

```csharp
public enum NullHandlingMode
{
    /// <summary>Null values are not written (attribute absent). Default.</summary>
    OmitNull,
    /// <summary>Null values written as { NULL = true }.</summary>
    ExplicitNull
}
```

Configurable globally via `DynamoDbExpressionConfig` or per-converter.

### 11. ExpressionValueEmitter (Shared Value Conversion for Expression Builders)

Filter (Spec 06), update (Spec 07), and key condition (Spec 13) builders all need to convert .NET values to `AttributeValue` for `ExpressionAttributeValues` dictionaries. Rather than each builder implementing its own conversion logic, they share a single internal component that applies the resolution order from Section 8 consistently.

```csharp
namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Converts .NET values to DynamoDB AttributeValue for use in expression builders.
/// Shared by FilterExpressionBuilder, UpdateExpressionBuilder, ConditionExpressionBuilder,
/// and KeyConditionExpressionBuilder to ensure consistent converter resolution.
/// </summary>
internal sealed class ExpressionValueEmitter
{
    private readonly IAttributeValueConverterRegistry registry;

    public ExpressionValueEmitter(IAttributeValueConverterRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Converts a .NET value to an AttributeValue, using the converter resolved
    /// for the given property. Applies the resolution order from Section 8:
    /// [DynamoDbConverter] on property → registry exact match → Nullable → Enum →
    /// open-generic collection → MissingConverterException.
    /// </summary>
    /// <param name="value">The .NET value to convert.</param>
    /// <param name="property">
    /// The PropertyInfo of the expression property being compared/set.
    /// Used to check for [DynamoDbConverter] attribute override (Section 8, step 1).
    /// May be null for literal values not tied to a property (e.g. Between bounds),
    /// in which case step 1 is skipped and resolution starts at step 2 using the
    /// runtime type of <paramref name="value"/>.
    /// </param>
    /// <returns>The DynamoDB AttributeValue representation.</returns>
    /// <exception cref="MissingConverterException">
    /// No converter found for the value's type (Spec 14 §3).
    /// </exception>
    public AttributeValue Emit(object value, PropertyInfo property)
    {
        var converter = ResolveConverter(value, property);
        return converter.ToAttributeValue(value);
    }

    private IAttributeValueConverter ResolveConverter(object value, PropertyInfo property)
    {
        // Step 1: Check [DynamoDbConverter] attribute on the property
        if (property != null)
        {
            var attr = property.GetCustomAttribute<DynamoDbConverterAttribute>();
            if (attr != null)
            {
                return (IAttributeValueConverter)Activator.CreateInstance(attr.ConverterType)!;
            }
        }

        // Steps 2–6: Delegate to registry (exact → Nullable → Enum → collection → throw)
        var targetType = property?.PropertyType ?? value.GetType();
        return registry.GetConverter(targetType);
    }
}
```

**Usage by expression builders:**

```csharp
// In FilterExpressionBuilder, when visiting p => p.Status == OrderStatus.Active:
//   Left side:  property path resolved via IAttributeNameResolverFactory
//   Right side: value emitted via ExpressionValueEmitter
var av = valueEmitter.Emit(OrderStatus.Active, statusPropertyInfo);
// → EnumConverter<OrderStatus>.ToAttributeValue(Active) → { S: "Active" }
// Stored as ":filt_v0" in ExpressionAttributeValues

// In UpdateExpressionBuilder, when calling .Set(p => p.Total, new Money(99.99m, "USD")):
//   [DynamoDbConverter(typeof(MoneyConverter))] on Total property
var av = valueEmitter.Emit(money, totalPropertyInfo);
// → MoneyConverter.ToAttributeValue(money) → { M: { "Amount": { N: "99.99" }, ... } }
// Stored as ":upd_v0" in ExpressionAttributeValues
```

**Thread safety:** `ExpressionValueEmitter` is stateless (no mutable fields) and safe for concurrent use. Expression builders hold a single shared instance.
