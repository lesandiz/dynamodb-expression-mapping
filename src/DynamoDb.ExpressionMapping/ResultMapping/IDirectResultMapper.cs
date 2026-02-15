using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.ResultMapping;

/// <summary>
/// Maps DynamoDB AttributeValue dictionaries directly to projected result types
/// without hydrating a full entity object.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// </summary>
public interface IDirectResultMapper<TSource>
{
    /// <summary>
    /// Creates a compiled, reusable mapper function for a given selector expression.
    /// The returned function maps directly from DynamoDB attributes to TResult.
    /// </summary>
    /// <typeparam name="TResult">The projected result type</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A compiled function that maps AttributeValue dictionaries to TResult</returns>
    Func<Dictionary<string, AttributeValue>, TResult> CreateMapper<TResult>(
        Expression<Func<TSource, TResult>> selector);

    /// <summary>
    /// One-shot mapping for single items. Uses cached mapper internally.
    /// </summary>
    TResult Map<TResult>(
        Dictionary<string, AttributeValue> attributes,
        Expression<Func<TSource, TResult>> selector);
}
