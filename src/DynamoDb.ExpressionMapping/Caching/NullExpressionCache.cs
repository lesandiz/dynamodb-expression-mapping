namespace DynamoDb.ExpressionMapping.Caching;

/// <summary>
/// No-op cache that always invokes the factory and never stores results.
/// Follows the NullLoggerFactory.Instance pattern.
/// Used in testing and scenarios where caching must be bypassed.
/// </summary>
public sealed class NullExpressionCache : IExpressionCache
{
    /// <summary>
    /// Singleton instance. Use this instead of creating new instances.
    /// </summary>
    public static readonly NullExpressionCache Instance = new();

    private NullExpressionCache() { }

    /// <summary>
    /// Always invokes the factory without caching the result.
    /// </summary>
    public TValue GetOrAdd<TValue>(string cacheCategory, string key, Func<string, TValue> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory(key);
    }
}
