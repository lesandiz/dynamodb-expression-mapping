using System.Collections.Concurrent;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Expressions;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Maps DynamoDB AttributeValue dictionaries directly to projected result types
/// without hydrating a full entity object. Uses expression compilation for
/// native performance after initial build.
/// </summary>
/// <typeparam name="TSource">The entity type whose attribute names are resolved</typeparam>
public sealed class DirectResultMapper<TSource> : IDirectResultMapper<TSource>
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;
    private readonly IExpressionCache _cache;
    private readonly Func<Dictionary<string, AttributeValue>, object>? _fullEntityMapper;

    // Compiled mapper cache: cache key → compiled delegate
    // Using ConcurrentDictionary for thread-safe caching without IExpressionCache abstraction
    // (IExpressionCache is for expression strings, not compiled delegates)
    private readonly ConcurrentDictionary<string, object> _compiledMappers = new();

    /// <summary>
    /// Creates a new DirectResultMapper instance.
    /// </summary>
    /// <param name="resolverFactory">Factory for resolving attribute names</param>
    /// <param name="converterRegistry">Registry for type converters</param>
    /// <param name="cache">Expression cache for metadata (optional)</param>
    /// <param name="fullEntityMapper">Optional fallback for identity projections (p => p)</param>
    public DirectResultMapper(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry,
        IExpressionCache? cache = null,
        Func<Dictionary<string, AttributeValue>, object>? fullEntityMapper = null)
    {
        _resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        _converterRegistry = converterRegistry ?? throw new ArgumentNullException(nameof(converterRegistry));
        _cache = cache ?? NullExpressionCache.Instance;
        _fullEntityMapper = fullEntityMapper;
    }

    /// <summary>
    /// Creates a compiled, reusable mapper function for a given selector expression.
    /// The returned function maps directly from DynamoDB attributes to TResult.
    /// </summary>
    public Func<Dictionary<string, AttributeValue>, TResult> CreateMapper<TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        // Generate cache key from expression structure
        var cacheKey = ExpressionKeyGenerator.GenerateKey(selector);

        // Check cache first
        if (_compiledMappers.TryGetValue(cacheKey, out var cached))
        {
            return (Func<Dictionary<string, AttributeValue>, TResult>)cached;
        }

        // Determine projection shape
        ProjectionExpressionVisitor.ExtractPropertyPaths(selector, out var shape);

        // Select appropriate strategy based on shape
        IMappingStrategy strategy = shape switch
        {
            ProjectionShape.Identity => new IdentityMappingStrategy(_fullEntityMapper),
            ProjectionShape.SingleProperty => new SinglePropertyMappingStrategy(
                _resolverFactory,
                _converterRegistry),
            ProjectionShape.Composite => new CompositeMappingStrategy(
                _resolverFactory,
                _converterRegistry),
            _ => throw new InvalidOperationException($"Unknown projection shape: {shape}")
        };

        // Build the mapper
        var mapper = strategy.BuildMapper(selector);

        // Cache the compiled mapper
        _compiledMappers.TryAdd(cacheKey, mapper);

        return mapper;
    }

    /// <summary>
    /// One-shot mapping for single items. Uses cached mapper internally.
    /// </summary>
    public TResult Map<TResult>(
        Dictionary<string, AttributeValue> attributes,
        Expression<Func<TSource, TResult>> selector)
    {
        if (attributes == null)
        {
            throw new ArgumentNullException(nameof(attributes));
        }

        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        var mapper = CreateMapper(selector);
        return mapper(attributes);
    }
}
