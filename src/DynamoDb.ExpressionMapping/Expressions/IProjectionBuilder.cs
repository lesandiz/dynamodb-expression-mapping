using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Builds DynamoDB ProjectionExpression strings from C# lambda expressions.
/// Generic over TSource — the entity type whose attribute names are resolved.
/// </summary>
public interface IProjectionBuilder<TSource>
{
    /// <summary>
    /// Builds a DynamoDB projection from a LINQ selector expression.
    /// </summary>
    /// <typeparam name="TResult">The projected result type</typeparam>
    /// <param name="selector">Lambda expression defining which properties to project</param>
    /// <returns>Projection result containing expression string, attribute names, and metadata</returns>
    ProjectionResult BuildProjection<TResult>(
        Expression<Func<TSource, TResult>> selector);
}
