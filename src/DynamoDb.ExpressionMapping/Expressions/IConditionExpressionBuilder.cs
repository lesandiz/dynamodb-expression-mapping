using System;
using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB ConditionExpression strings from C# lambda predicates.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// </summary>
/// <typeparam name="TSource">The entity type to build condition expressions for.</typeparam>
public interface IConditionExpressionBuilder<TSource>
{
    /// <summary>
    /// Builds a DynamoDB condition expression from a predicate.
    /// </summary>
    /// <param name="predicate">A lambda expression representing a boolean condition.</param>
    /// <returns>A ConditionExpressionResult containing the expression string and attribute mappings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="predicate"/> is null.</exception>
    /// <exception cref="UnsupportedExpressionException">Thrown when the predicate contains unsupported operations.</exception>
    /// <exception cref="InvalidConditionException">Thrown when the predicate is invalid (e.g., non-boolean expression, uses ignored properties).</exception>
    ConditionExpressionResult BuildCondition(Expression<Func<TSource, bool>> predicate);
}
