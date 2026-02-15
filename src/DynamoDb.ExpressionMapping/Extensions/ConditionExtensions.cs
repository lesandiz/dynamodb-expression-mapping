using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Extension methods for applying condition expressions to DynamoDB SDK requests.
/// </summary>
public static class ConditionExtensions
{
    /// <summary>
    /// Applies a condition expression to a PutItemRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being written.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="conditionBuilder">The condition expression builder instance.</param>
    /// <param name="predicate">The condition predicate expression.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static PutItemRequest WithCondition<TSource>(
        this PutItemRequest request,
        IConditionExpressionBuilder<TSource> conditionBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(conditionBuilder);

        var result = conditionBuilder.BuildCondition(predicate);
        return request.ApplyCondition(result);
    }

    /// <summary>
    /// Applies a condition expression to a DeleteItemRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being deleted.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="conditionBuilder">The condition expression builder instance.</param>
    /// <param name="predicate">The condition predicate expression.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static DeleteItemRequest WithCondition<TSource>(
        this DeleteItemRequest request,
        IConditionExpressionBuilder<TSource> conditionBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(conditionBuilder);

        var result = conditionBuilder.BuildCondition(predicate);
        return request.ApplyCondition(result);
    }

    /// <summary>
    /// Applies a condition expression to an UpdateItemRequest.
    /// </summary>
    /// <typeparam name="TSource">The entity type being updated.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="conditionBuilder">The condition expression builder instance.</param>
    /// <param name="predicate">The condition predicate expression.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder is null.</exception>
    public static UpdateItemRequest WithCondition<TSource>(
        this UpdateItemRequest request,
        IConditionExpressionBuilder<TSource> conditionBuilder,
        Expression<Func<TSource, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(conditionBuilder);

        var result = conditionBuilder.BuildCondition(predicate);
        return request.ApplyCondition(result);
    }
}
