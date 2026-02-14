namespace DynamoDb.ExpressionMapping.Caching;

/// <summary>
/// Diagnostic statistics for cache performance analysis.
/// </summary>
public sealed class CacheStatistics
{
    /// <summary>
    /// Number of cache hits for projection expressions.
    /// </summary>
    public int ProjectionHits { get; init; }

    /// <summary>
    /// Number of cache misses for projection expressions.
    /// </summary>
    public int ProjectionMisses { get; init; }

    /// <summary>
    /// Number of cache hits for mapper delegates.
    /// </summary>
    public int MapperHits { get; init; }

    /// <summary>
    /// Number of cache misses for mapper delegates.
    /// </summary>
    public int MapperMisses { get; init; }

    /// <summary>
    /// Number of cache hits for filter expressions.
    /// </summary>
    public int FilterHits { get; init; }

    /// <summary>
    /// Number of cache misses for filter expressions.
    /// </summary>
    public int FilterMisses { get; init; }

    /// <summary>
    /// Total number of cached entries across all categories.
    /// </summary>
    public int TotalEntries { get; init; }

    /// <summary>
    /// Projection cache hit rate (0.0 to 1.0).
    /// </summary>
    public double ProjectionHitRate =>
        ProjectionHits + ProjectionMisses > 0
            ? (double)ProjectionHits / (ProjectionHits + ProjectionMisses)
            : 0.0;

    /// <summary>
    /// Mapper cache hit rate (0.0 to 1.0).
    /// </summary>
    public double MapperHitRate =>
        MapperHits + MapperMisses > 0
            ? (double)MapperHits / (MapperHits + MapperMisses)
            : 0.0;

    /// <summary>
    /// Filter cache hit rate (0.0 to 1.0).
    /// </summary>
    public double FilterHitRate =>
        FilterHits + FilterMisses > 0
            ? (double)FilterHits / (FilterHits + FilterMisses)
            : 0.0;

    /// <summary>
    /// Overall cache hit rate across all categories (0.0 to 1.0).
    /// </summary>
    public double OverallHitRate
    {
        get
        {
            var totalHits = ProjectionHits + MapperHits + FilterHits;
            var totalRequests = ProjectionHits + ProjectionMisses + MapperHits + MapperMisses + FilterHits + FilterMisses;
            return totalRequests > 0 ? (double)totalHits / totalRequests : 0.0;
        }
    }
}
