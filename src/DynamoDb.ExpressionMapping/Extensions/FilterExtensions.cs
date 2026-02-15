using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Extension methods for applying filter expressions to DynamoDB SDK requests.
/// </summary>
public static class FilterExtensions
{
    /// <summary>
    /// Applies a filter expression to a QueryRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being queried.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="filterBuilder">The filter expression builder instance.</param>
    /// <param name="predicate">The filter predicate expression.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static QueryRequest WithFilter<TSource>(
        this QueryRequest request,
        IFilterExpressionBuilder<TSource> filterBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(filterBuilder);

        var result = filterBuilder.BuildFilter(predicate);
        return request.ApplyFilter(result);
    }

    /// <summary>
    /// Applies a filter expression to a ScanRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being scanned.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="filterBuilder">The filter expression builder instance.</param>
    /// <param name="predicate">The filter predicate expression.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static ScanRequest WithFilter<TSource>(
        this ScanRequest request,
        IFilterExpressionBuilder<TSource> filterBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(filterBuilder);

        var result = filterBuilder.BuildFilter(predicate);
        return request.ApplyFilter(result);
    }
}
