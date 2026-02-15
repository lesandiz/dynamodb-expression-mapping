using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Combined extension methods for applying multiple expression types in a single call.
/// </summary>
public static class CombinedExtensions
{
    /// <summary>
    /// Applies both projection and filter expressions to a QueryRequest in one call.
    /// Convenience method equivalent to calling WithProjection() followed by WithFilter().
    /// </summary>
    /// <typeparam name="TSource">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The result type of the projection selector.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="projectionBuilder">The projection builder instance.</param>
    /// <param name="selector">The projection selector expression.</param>
    /// <param name="filterBuilder">The filter expression builder instance.</param>
    /// <param name="predicate">The filter predicate expression.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if any builder is null.</exception>
    public static QueryRequest WithExpressions<TSource, TResult>(
        this QueryRequest request,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>> selector,
        IFilterExpressionBuilder<TSource> filterBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        return request
            .WithProjection(projectionBuilder, selector)
            .WithFilter(filterBuilder, predicate);
    }
}
