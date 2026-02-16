using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.SoakTests;

/// <summary>
/// Holds shared DI instances for all workers to simulate real DI container scoping.
/// All workers share the same instances to test thread-safety of caches and registries.
/// Per PR-02.2: Workers share DynamoDbExpressionConfig, ExpressionCache, and AttributeValueConverterRegistry.
/// </summary>
public sealed class SharedDependencies
{
    /// <summary>
    /// Shared attribute name resolver factory (thread-safe, read-only after construction).
    /// </summary>
    public IAttributeNameResolverFactory ResolverFactory { get; }

    /// <summary>
    /// Shared attribute value converter registry (thread-safe for reads).
    /// </summary>
    public AttributeValueConverterRegistry ConverterRegistry { get; }

    /// <summary>
    /// Shared expression cache (must be thread-safe).
    /// </summary>
    public ExpressionCache ExpressionCache { get; }

    public SharedDependencies()
    {
        ResolverFactory = new AttributeNameResolverFactory();
        ConverterRegistry = AttributeValueConverterRegistry.Default;
        ExpressionCache = ExpressionCache.Default;
    }

    /// <summary>
    /// Gets current cache statistics across all expression types.
    /// </summary>
    public CacheStatistics GetCacheStatistics() => ExpressionCache.GetStatistics();
}
