using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Extension methods for applying projection expressions to DynamoDB SDK requests.
/// </summary>
public static class ProjectionExtensions
{
    /// <summary>
    /// Applies a projection expression to a GetItemRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The result type of the projection selector.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="projectionBuilder">The projection builder instance.</param>
    /// <param name="selector">The projection selector expression.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static GetItemRequest WithProjection<TSource, TResult>(
        this GetItemRequest request,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(projectionBuilder);

        var result = projectionBuilder.BuildProjection(selector);
        return request.ApplyProjection(result);
    }

    /// <summary>
    /// Applies a projection expression to a QueryRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The result type of the projection selector.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="projectionBuilder">The projection builder instance.</param>
    /// <param name="selector">The projection selector expression, or null to select all attributes.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static QueryRequest WithProjection<TSource, TResult>(
        this QueryRequest request,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>>? selector)
    {
        ArgumentNullException.ThrowIfNull(projectionBuilder);

        if (selector == null) return request;
        var result = projectionBuilder.BuildProjection(selector);
        return request.ApplyProjection(result);
    }

    /// <summary>
    /// Applies a projection expression to a ScanRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being scanned.</typeparam>
    /// <typeparam name="TResult">The result type of the projection selector.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="projectionBuilder">The projection builder instance.</param>
    /// <param name="selector">The projection selector expression, or null to select all attributes.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static ScanRequest WithProjection<TSource, TResult>(
        this ScanRequest request,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>>? selector)
    {
        ArgumentNullException.ThrowIfNull(projectionBuilder);

        if (selector == null) return request;
        var result = projectionBuilder.BuildProjection(selector);
        return request.ApplyProjection(result);
    }

    /// <summary>
    /// Applies a projection expression to a specific table in a BatchGetItemRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The result type of the projection selector.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="tableName">The table name within RequestItems to apply the projection to.</param>
    /// <param name="projectionBuilder">The projection builder instance.</param>
    /// <param name="selector">The projection selector expression, or null to select all attributes.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null or RequestItems is null.</exception>
    /// <exception cref="ArgumentException">Thrown if tableName is not found in RequestItems.</exception>
    public static BatchGetItemRequest WithProjection<TSource, TResult>(
        this BatchGetItemRequest request,
        string tableName,
        IProjectionBuilder<TSource> projectionBuilder,
        Expression<Func<TSource, TResult>>? selector)
    {
        ArgumentNullException.ThrowIfNull(projectionBuilder);

        if (selector == null) return request;

        ArgumentNullException.ThrowIfNull(request.RequestItems);

        if (!request.RequestItems.TryGetValue(tableName, out var keysAndAttributes))
            throw new ArgumentException(
                $"Table '{tableName}' not found in RequestItems.", nameof(tableName));

        var result = projectionBuilder.BuildProjection(selector);
        request.RequestItems[tableName] = keysAndAttributes.ApplyProjection(result);
        return request;
    }
}
