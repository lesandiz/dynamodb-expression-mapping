namespace DynamoDb.ExpressionMapping.Caching;

/// <summary>
/// Abstraction for expression analysis caching.
/// Caches expensive operations like expression tree traversal and delegate compilation.
/// </summary>
public interface IExpressionCache
{
    /// <summary>
    /// Gets or adds a value to the cache.
    /// </summary>
    /// <typeparam name="TValue">The type of the cached value.</typeparam>
    /// <param name="cacheCategory">The cache category (e.g., "projection", "mapper", "filter").</param>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory function to create the value if not cached.</param>
    /// <returns>The cached or newly created value.</returns>
    TValue GetOrAdd<TValue>(string cacheCategory, string key, Func<string, TValue> factory);
}
