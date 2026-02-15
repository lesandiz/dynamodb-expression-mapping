using System;
using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB FilterExpression strings from C# lambda predicates.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// </summary>
/// <typeparam name="TSource">The entity type to build filter expressions for.</typeparam>
public interface IFilterExpressionBuilder<TSource>
{
    /// <summary>
    /// Builds a DynamoDB filter expression from a predicate.
    /// </summary>
    /// <param name="predicate">A lambda expression representing a boolean filter condition.</param>
    /// <returns>A FilterExpressionResult containing the expression string and attribute mappings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the predicate contains unsupported operations.</exception>
    /// <exception cref="InvalidFilterException">Thrown when the predicate is invalid (e.g., non-boolean expression, uses ignored properties).</exception>
    FilterExpressionResult BuildFilter(Expression<Func<TSource, bool>> predicate);
}
