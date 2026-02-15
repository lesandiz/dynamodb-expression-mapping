using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Extension methods for applying update expressions to DynamoDB SDK requests.
/// </summary>
public static class UpdateExtensions
{
    /// <summary>
    /// Applies an update expression to an UpdateItemRequest.
    /// </summary>
    /// <param name="request">The request to modify.</param>
    /// <param name="updateResult">The update expression result to apply.</param>
    /// <returns>The modified request for fluent chaining.</returns>
    public static UpdateItemRequest WithUpdate(
        this UpdateItemRequest request,
        UpdateExpressionResult updateResult)
    {
        if (updateResult.IsEmpty) return request;

        request.UpdateExpression = updateResult.Expression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();

        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            updateResult.ExpressionAttributeNames);
        RequestMergeHelpers.MergeAttributeValues(
            request.ExpressionAttributeValues,
            updateResult.ExpressionAttributeValues);

        return request;
    }
}
