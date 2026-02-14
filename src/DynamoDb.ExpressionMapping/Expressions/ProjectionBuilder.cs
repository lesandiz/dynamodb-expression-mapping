using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using DynamoDb.ExpressionMapping.Caching;
using DynamoDb.ExpressionMapping.Exceptions;
using DynamoDb.ExpressionMapping.Mapping;
using DynamoDb.ExpressionMapping.ReservedKeywords;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB ProjectionExpression strings from C# lambda expressions.
/// </summary>
public sealed class ProjectionBuilder<TSource> : IProjectionBuilder<TSource>
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly ReservedKeywordRegistry _reservedKeywords;
    private readonly IExpressionCache _cache;
    private readonly NameResolutionMode _resolutionMode;

    /// <summary>
    /// Creates a projection builder with a resolver factory for cross-type
    /// nested path resolution, and optional overrides.
    /// </summary>
    public ProjectionBuilder(
        IAttributeNameResolverFactory resolverFactory,
        ReservedKeywordRegistry? reservedKeywords = null,
        IExpressionCache? cache = null,
        NameResolutionMode resolutionMode = NameResolutionMode.Strict)
    {
        _resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
        _reservedKeywords = reservedKeywords ?? ReservedKeywordRegistry.Default;
        _cache = cache ?? ExpressionCache.Default;
        _resolutionMode = resolutionMode;
    }

    /// <summary>
    /// Builds a DynamoDB projection from a LINQ selector expression.
    /// </summary>
    public ProjectionResult BuildProjection<TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        if (selector == null)
            throw new ArgumentNullException(nameof(selector));

        // Check cache
        var cacheKey = ExpressionKeyGenerator.GenerateKey(selector);
        return _cache.GetOrAdd("projection", cacheKey, _ => BuildProjectionCore(selector));
    }

    private ProjectionResult BuildProjectionCore<TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        // Extract property paths and shape
        var paths = ProjectionExpressionVisitor.ExtractPropertyPaths(selector, out var shape);

        // Empty paths = identity (whole object)
        if (paths.Count == 0)
            return ProjectionResult.Empty;

        var aliasGenerator = new AliasGenerator("proj");
        var attributeNames = new Dictionary<string, string>();
        var fragments = new List<string>();
        var resolvedNames = new List<string>();

        foreach (var path in paths)
        {
            var fragment = BuildPathFragment(path, aliasGenerator, attributeNames, resolvedNames);
            if (!string.IsNullOrEmpty(fragment))
                fragments.Add(fragment);
        }

        var projectionExpression = string.Join(", ", fragments);

        return new ProjectionResult(
            projectionExpression,
            attributeNames,
            paths,
            shape,
            resolvedNames);
    }

    private string BuildPathFragment(
        PropertyPath path,
        AliasGenerator aliasGenerator,
        Dictionary<string, string> attributeNames,
        List<string> resolvedNames)
    {
        var segments = path.Segments;
        var segmentProperties = path.SegmentProperties;
        var resolvedSegments = new List<string>();

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var propertyInfo = segmentProperties[i];
            var declaringType = propertyInfo.DeclaringType!;

            // Get resolver for this type
            var resolver = _resolverFactory.GetResolver(declaringType);

            // Check if this is a stored attribute
            if (!resolver.IsStoredAttribute(segment))
            {
                if (_resolutionMode == NameResolutionMode.Strict)
                {
                    throw new InvalidProjectionException(segment, declaringType);
                }
                // Lenient mode: skip this entire path
                return string.Empty;
            }

            // Resolve the attribute name
            var attributeName = resolver.GetAttributeName(segment);

            if (string.IsNullOrEmpty(attributeName))
                throw new InvalidOperationException($"Resolved attribute name for '{segment}' is null or empty.");

            // Check if aliasing is needed
            if (NeedsAliasing(attributeName))
            {
                var alias = aliasGenerator.NextName();
                attributeNames[alias] = attributeName;
                resolvedSegments.Add(alias);
            }
            else
            {
                resolvedSegments.Add(attributeName);
            }

            // Track resolved name (only for the top-level segment)
            if (i == 0)
                resolvedNames.Add(attributeName);
        }

        // Join segments with dots for nested paths
        return string.Join(".", resolvedSegments);
    }

    private bool NeedsAliasing(string attributeName)
    {
        return _reservedKeywords.IsReserved(attributeName) || ContainsSpecialCharacters(attributeName);
    }

    private static bool ContainsSpecialCharacters(string name)
    {
        // DynamoDB requires aliasing for attribute names with special characters
        // Check for characters that aren't alphanumeric, underscore, or hyphen
        return !Regex.IsMatch(name, @"^[a-zA-Z0-9_-]+$");
    }
}
