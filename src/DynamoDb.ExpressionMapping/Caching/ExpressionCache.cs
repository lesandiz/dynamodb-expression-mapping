using System.Collections.Concurrent;

namespace DynamoDb.ExpressionMapping.Caching;

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

    private readonly ConcurrentDictionary<string, object> _projectionCache;
    private readonly ConcurrentDictionary<string, object> _mapperCache;
    private readonly ConcurrentDictionary<string, object> _filterCache;

    // Statistics tracking
    private int _projectionHits;
    private int _projectionMisses;
    private int _mapperHits;
    private int _mapperMisses;
    private int _filterHits;
    private int _filterMisses;

    /// <summary>
    /// Creates a new expression cache.
    /// </summary>
    /// <param name="maxSize">Optional maximum size. Default is unbounded (safe for expression shapes).</param>
    public ExpressionCache(int? maxSize = null)
    {
        // In a typical application, there are tens to low hundreds of unique selector shapes.
        // Expression shapes are finite and determined at compile time.
        // Unbounded cache is safe for this use case.

        _projectionCache = new ConcurrentDictionary<string, object>();
        _mapperCache = new ConcurrentDictionary<string, object>();
        _filterCache = new ConcurrentDictionary<string, object>();

        // Note: maxSize parameter is reserved for future LRU/size-based eviction if needed
        // Current implementation uses unbounded cache per spec design rationale
    }

    /// <summary>
    /// Gets or adds a value to the cache.
    /// </summary>
    public TValue GetOrAdd<TValue>(string cacheCategory, string key, Func<string, TValue> factory)
    {
        ArgumentNullException.ThrowIfNull(cacheCategory);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(factory);

        var cache = GetCacheForCategory(cacheCategory);

        // Check if key exists first (for statistics tracking)
        var isHit = cache.ContainsKey(key);

        // Track statistics
        TrackAccess(cacheCategory, isHit);

        // GetOrAdd ensures thread-safe single computation per key
        var result = cache.GetOrAdd(key, k => factory(k)!);

        return (TValue)result;
    }

    /// <summary>
    /// Gets cache statistics for diagnostic purposes.
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            ProjectionHits = _projectionHits,
            ProjectionMisses = _projectionMisses,
            MapperHits = _mapperHits,
            MapperMisses = _mapperMisses,
            FilterHits = _filterHits,
            FilterMisses = _filterMisses,
            TotalEntries = _projectionCache.Count + _mapperCache.Count + _filterCache.Count
        };
    }

    /// <summary>
    /// Clears all cached entries. Useful for testing or memory management.
    /// </summary>
    public void Clear()
    {
        _projectionCache.Clear();
        _mapperCache.Clear();
        _filterCache.Clear();

        // Reset statistics
        _projectionHits = 0;
        _projectionMisses = 0;
        _mapperHits = 0;
        _mapperMisses = 0;
        _filterHits = 0;
        _filterMisses = 0;
    }

    private ConcurrentDictionary<string, object> GetCacheForCategory(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "projection" => _projectionCache,
            "mapper" => _mapperCache,
            "filter" => _filterCache,
            _ => throw new ArgumentException($"Unknown cache category: {category}", nameof(category))
        };
    }

    private void TrackAccess(string category, bool isHit)
    {
        switch (category.ToLowerInvariant())
        {
            case "projection":
                if (isHit)
                    Interlocked.Increment(ref _projectionHits);
                else
                    Interlocked.Increment(ref _projectionMisses);
                break;
            case "mapper":
                if (isHit)
                    Interlocked.Increment(ref _mapperHits);
                else
                    Interlocked.Increment(ref _mapperMisses);
                break;
            case "filter":
                if (isHit)
                    Interlocked.Increment(ref _filterHits);
                else
                    Interlocked.Increment(ref _filterMisses);
                break;
        }
    }
}
