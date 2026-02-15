using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Exceptions;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Handles identity projections: p => p or p => (p)
/// Delegates to a consumer-provided full entity mapper.
/// </summary>
internal sealed class IdentityMappingStrategy : IMappingStrategy
{
    private readonly Func<Dictionary<string, AttributeValue>, object>? _fullEntityMapper;

    public IdentityMappingStrategy(Func<Dictionary<string, AttributeValue>, object>? fullEntityMapper)
    {
        _fullEntityMapper = fullEntityMapper;
    }

    public Func<Dictionary<string, AttributeValue>, TResult> BuildMapper<TSource, TResult>(
        Expression<Func<TSource, TResult>> selector)
    {
        if (_fullEntityMapper == null)
        {
            throw new UnsupportedExpressionException(
                selector.Body.NodeType,
                selector.ToString());
        }

        // Cast the full entity mapper result to TResult
        return attrs => (TResult)_fullEntityMapper(attrs);
    }
}
