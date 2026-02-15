using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Expressions;

namespace DynamoDb.ExpressionMapping.Extensions;

/// <summary>
/// Internal extension methods for applying expression results to SDK requests.
/// </summary>
internal static class InternalRequestExtensions
{
    /// <summary>
    /// Applies a projection result to a GetItemRequest.
    /// </summary>
    internal static GetItemRequest ApplyProjection(
        this GetItemRequest request, ProjectionResult result)
    {
        if (result.IsEmpty) return request;

        request.ProjectionExpression = result.ProjectionExpression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);

        return request;
    }

    /// <summary>
    /// Applies a projection result to a QueryRequest.
    /// </summary>
    internal static QueryRequest ApplyProjection(
        this QueryRequest request, ProjectionResult result)
    {
        if (result.IsEmpty) return request;

        request.ProjectionExpression = result.ProjectionExpression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);

        return request;
    }

    /// <summary>
    /// Applies a projection result to a ScanRequest.
    /// </summary>
    internal static ScanRequest ApplyProjection(
        this ScanRequest request, ProjectionResult result)
    {
        if (result.IsEmpty) return request;

        request.ProjectionExpression = result.ProjectionExpression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);

        return request;
    }

    /// <summary>
    /// Applies a projection result to a KeysAndAttributes object.
    /// </summary>
    internal static KeysAndAttributes ApplyProjection(
        this KeysAndAttributes keysAndAttributes, ProjectionResult result)
    {
        if (result.IsEmpty) return keysAndAttributes;

        keysAndAttributes.ProjectionExpression = result.ProjectionExpression;
        keysAndAttributes.ExpressionAttributeNames ??= new Dictionary<string, string>();
        RequestMergeHelpers.MergeAttributeNames(
            keysAndAttributes.ExpressionAttributeNames,
            result.ExpressionAttributeNames);

        return keysAndAttributes;
    }

    /// <summary>
    /// Applies a filter result to a QueryRequest.
    /// </summary>
    internal static QueryRequest ApplyFilter(
        this QueryRequest request, FilterExpressionResult result)
    {
        if (result.IsEmpty) return request;

        request.FilterExpression = result.Expression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);
        RequestMergeHelpers.MergeAttributeValues(
            request.ExpressionAttributeValues,
            result.ExpressionAttributeValues);

        return request;
    }

    /// <summary>
    /// Applies a filter result to a ScanRequest.
    /// </summary>
    internal static ScanRequest ApplyFilter(
        this ScanRequest request, FilterExpressionResult result)
    {
        if (result.IsEmpty) return request;

        request.FilterExpression = result.Expression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);
        RequestMergeHelpers.MergeAttributeValues(
            request.ExpressionAttributeValues,
            result.ExpressionAttributeValues);

        return request;
    }

    /// <summary>
    /// Applies a condition result to a PutItemRequest.
    /// </summary>
    internal static PutItemRequest ApplyCondition(
        this PutItemRequest request, ConditionExpressionResult result)
    {
        if (result.IsEmpty) return request;

        request.ConditionExpression = result.Expression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);
        RequestMergeHelpers.MergeAttributeValues(
            request.ExpressionAttributeValues,
            result.ExpressionAttributeValues);

        return request;
    }

    /// <summary>
    /// Applies a condition result to a DeleteItemRequest.
    /// </summary>
    internal static DeleteItemRequest ApplyCondition(
        this DeleteItemRequest request, ConditionExpressionResult result)
    {
        if (result.IsEmpty) return request;

        request.ConditionExpression = result.Expression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);
        RequestMergeHelpers.MergeAttributeValues(
            request.ExpressionAttributeValues,
            result.ExpressionAttributeValues);

        return request;
    }

    /// <summary>
    /// Applies a condition result to an UpdateItemRequest.
    /// </summary>
    internal static UpdateItemRequest ApplyCondition(
        this UpdateItemRequest request, ConditionExpressionResult result)
    {
        if (result.IsEmpty) return request;

        request.ConditionExpression = result.Expression;
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames,
            result.ExpressionAttributeNames);
        RequestMergeHelpers.MergeAttributeValues(
            request.ExpressionAttributeValues,
            result.ExpressionAttributeValues);

        return request;
    }

    /// <summary>
    /// Merges attribute names into the request's ExpressionAttributeNames dictionary.
    /// </summary>
    internal static void MergeAttributeNames(
        this QueryRequest request,
        IReadOnlyDictionary<string, string> names)
    {
        request.ExpressionAttributeNames ??= new Dictionary<string, string>();
        RequestMergeHelpers.MergeAttributeNames(
            request.ExpressionAttributeNames, names);
    }

    /// <summary>
    /// Merges attribute values into the request's ExpressionAttributeValues dictionary.
    /// </summary>
    internal static void MergeAttributeValues(
        this QueryRequest request,
        IReadOnlyDictionary<string, AttributeValue> values)
    {
        request.ExpressionAttributeValues ??= new Dictionary<string, AttributeValue>();
        RequestMergeHelpers.MergeAttributeValues(
            request.ExpressionAttributeValues, values);
    }
}
