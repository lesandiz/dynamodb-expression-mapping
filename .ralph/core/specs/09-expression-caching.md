# Spec 09: Expression Caching

## Motivation

Expression tree analysis (visitor traversal, projection building, delegate compilation) is computationally expensive relative to the DynamoDB operation itself. In a typical application, the same selector expressions are used repeatedly across requests. Caching avoids redundant work.

Two specific costs to cache:
1. **Projection analysis** — Expression visitor + string builder + reserved keyword checks
2. **Delegate compilation** — `Expression.Compile()` for result mapper delegates

## Design

### 1. ExpressionCache

```csharp
namespace DynamoDb.ExpressionMapping.Caching;

/// <summary>
/// Abstraction for expression analysis caching.
/// </summary>
public interface IExpressionCache
{
    TValue GetOrAdd<TValue>(string cacheCategory, string key, Func<string, TValue> factory);
}

/// <summary>
/// Thread-safe cache for expression analysis results and compiled delegates.
/// Avoids redundant expression tree traversal and delegate compilation.
/// </summary>
public sealed class ExpressionCache : IExpressionCache
{
    /// <summary>
    /// Default shared cache instance. Suitable for most use cases.
    /// </summary>
    public static readonly ExpressionCache Default = new();

    private readonly ConcurrentDictionary<string, object> projectionCache;
    private readonly ConcurrentDictionary<string, object> mapperCache;
    private readonly ConcurrentDictionary<string, object> filterCache;

    public ExpressionCache(int? maxSize = null)
    {
        // maxSize controls eviction (LRU or size-based)
        // Default: unbounded (expressions are typically finite in a codebase)
    }
}

/// <summary>
/// No-op cache that always invokes the factory. Never stores results.
/// Follows the NullLoggerFactory.Instance pattern.
/// Used in testing and scenarios where caching must be bypassed.
/// </summary>
public sealed class NullExpressionCache : IExpressionCache
{
    public static readonly NullExpressionCache Instance = new();

    private NullExpressionCache() { }

    public TValue GetOrAdd<TValue>(string cacheCategory, string key, Func<string, TValue> factory)
        => factory(key);
}
```

### 2. Cache Key Generation

Expression trees cannot be compared by reference (two identical lambdas at different call sites are different objects). The cache needs structural comparison:

```csharp
namespace DynamoDb.ExpressionMapping.Caching;

/// <summary>
/// Generates a structural hash key for expression trees.
/// Two expressions with the same structure produce the same key,
/// regardless of where they were defined.
/// </summary>
public static class ExpressionKeyGenerator
{
    /// <summary>
    /// Generates a cache key from an expression tree's structure.
    /// Ignores captured variable VALUES (cache is shape-based, not value-based).
    /// </summary>
    public static string GenerateKey<TSource, TResult>(
        Expression<Func<TSource, TResult>> expression)
    {
        // Uses Expression.ToString() as a baseline (captures structure)
        // Prefixed with source and result type names for disambiguation
        return $"{typeof(TSource).Name}→{typeof(TResult).Name}:{expression}";
    }
}
```

### 3. What Gets Cached

| Artefact | Cache Key | Cached Value | Lifetime |
|---|---|---|---|
| `ProjectionResult` | Expression structure key | `ProjectionResult` | App lifetime |
| Compiled mapper delegate | Expression structure key | `Func<Dict<string,AV>, TResult>` | App lifetime |
| `FilterExpressionResult` | N/A — filters often have captured variables | Not cached by default | N/A |
| Property paths | Expression structure key | `IReadOnlyList<PropertyPath>` | App lifetime |

Filters are typically NOT cached because they often contain captured runtime values (e.g. `p => p.CreatedAt > someVariable`). The expression structure is the same but the value changes. Projection selectors, by contrast, are purely structural with no runtime values.

### 4. Cache Integration Points

#### ProjectionBuilder\<TSource\>

```csharp
public ProjectionResult BuildProjection<TResult>(
    Expression<Func<TSource, TResult>> selector)
{
    var key = ExpressionKeyGenerator.GenerateKey(selector);

    return (ProjectionResult)this.cache.projectionCache.GetOrAdd(key, _ =>
    {
        return BuildProjectionInternal(selector);
    });
}
```

#### DirectResultMapper\<TSource\>

```csharp
public Func<Dictionary<string, AttributeValue>, TResult> CreateMapper<TResult>(
    Expression<Func<TSource, TResult>> selector)
{
    var key = ExpressionKeyGenerator.GenerateKey(selector);

    return (Func<Dictionary<string, AttributeValue>, TResult>)this.cache.mapperCache.GetOrAdd(key, _ =>
    {
        return BuildMapperInternal<TResult>(selector);
    });
}
```

### 5. Cache Bypass

For testing and edge cases, support bypassing the cache:

```csharp
var builder = new ProjectionBuilder<Order>(resolver, cache: NullExpressionCache.Instance);
```

`NullExpressionCache.Instance` is a no-op `IExpressionCache` implementation that always invokes the factory and never stores results. Follows the `NullLoggerFactory.Instance` pattern from `Microsoft.Extensions.Logging`.

### 6. Cache Statistics (Optional)

For diagnostics:

```csharp
public sealed class CacheStatistics
{
    public int ProjectionHits { get; }
    public int ProjectionMisses { get; }
    public int MapperHits { get; }
    public int MapperMisses { get; }
    public int TotalEntries { get; }
}

var stats = ExpressionCache.Default.GetStatistics();
```

### 7. Memory Considerations

- Expression trees themselves are NOT cached (they are provided by the caller)
- Only analysis results and compiled delegates are cached
- Compiled delegates are small (a few KB each)
- `ProjectionResult` is small (a string + small dictionary)
- In a typical application, there are tens to low hundreds of unique selector shapes
- Unbounded cache is safe: expression shapes are finite and determined at compile time

### 8. Thread Safety

- `ConcurrentDictionary` for all cache stores
- `GetOrAdd` with factory delegate ensures single computation per key
- All cached values are immutable (`ProjectionResult`, `Func<>` delegates)
- No locking needed beyond what `ConcurrentDictionary` provides

### 9. Key Correctness

The key generation must distinguish structurally different expressions:

```csharp
// These must have DIFFERENT keys:
p => p.OrderId                              // returns Guid
p => new { p.OrderId }                      // returns anonymous type with Guid
p => new { Id = p.OrderId }                 // different member name
p => new OrderDto { Id = p.OrderId }        // returns named type

// These must have THE SAME key (defined at different call sites):
Expression<Func<Order, Guid>> selector1 = p => p.OrderId;
Expression<Func<Order, Guid>> selector2 = p => p.OrderId;
```

`Expression.ToString()` handles this correctly — it produces the same string for structurally identical expressions regardless of call site.
