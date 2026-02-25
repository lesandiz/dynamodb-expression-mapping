using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Handles composite projections by rewriting the selector expression tree:
/// replaces source parameter property accesses with dictionary reads while
/// preserving all other nodes (method calls, constructors, member inits, casts).
/// </summary>
internal sealed class CompositeMappingStrategy : IMappingStrategy
{
    private readonly IAttributeNameResolverFactory _resolverFactory;
    private readonly IAttributeValueConverterRegistry _converterRegistry;

    public CompositeMappingStrategy(
        IAttributeNameResolverFactory resolverFactory,
        IAttributeValueConverterRegistry converterRegistry)
    {
        _resolverFactory = resolverFactory;
        _converterRegistry = converterRegistry;
    }

    public Func<Dictionary<string, AttributeValue>, TResult> BuildMapper<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        var sourceParam = selector.Parameters[0];
        var attrsParam = Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "attrs");

        var visitor = new SelectorRewritingVisitor(
            sourceParam, attrsParam, typeof(TSource),
            _resolverFactory, _converterRegistry);

        var rewrittenBody = visitor.Visit(selector.Body);

        var lambda = Expression.Lambda<Func<Dictionary<string, AttributeValue>, TResult>>(
            rewrittenBody, attrsParam);

        return lambda.Compile();
    }
}
