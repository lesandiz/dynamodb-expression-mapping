using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Extension methods for applying key condition expressions to DynamoDB SDK requests.
/// </summary>
public static class KeyConditionExtensions
{
    /// <summary>
    /// Applies a key condition expression to a QueryRequest using a staged fluent API.
    /// </summary>
    /// <typeparam name="TSource">The entity type being queried.</typeparam>
    /// <param name="request">The request to modify.</param>
    /// <param name="keyConditionBuilder">The key condition expression builder instance.</param>
    /// <param name="configure">
    /// A function that accepts the builder and returns a KeyConditionExpressionResult
    /// by calling the staged fluent API (WithPartitionKey → sort key method → result).
    /// </param>
    /// <returns>The modified request for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if builder or configure is null.</exception>
    /// <example>
    /// <code>
    /// .WithKeyCondition(keyConditionBuilder,
    ///     b => b.WithPartitionKey(o => o.PK, "USER#123")
    ///           .WithSortKeyBeginsWith(o => o.SK, "ORDER#"))
    /// </code>
    /// </example>
    public static QueryRequest WithKeyCondition<TSource>(
        this QueryRequest request,
        IKeyConditionExpressionBuilder<TSource> keyConditionBuilder,
        Func<IKeyConditionExpressionBuilder<TSource>, KeyConditionExpressionResult> configure)
    {
        ArgumentNullException.ThrowIfNull(keyConditionBuilder);
        ArgumentNullException.ThrowIfNull(configure);

        var result = configure(keyConditionBuilder);

        request.KeyConditionExpression = result.Expression;
        request.MergeAttributeNames(result.ExpressionAttributeNames);
        request.MergeAttributeValues(result.ExpressionAttributeValues);

        return request;
    }
}
