using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Represents a compiled DynamoDB KeyConditionExpression with attribute name and value mappings.
/// Immutable result object returned by <see cref="ISortKeyConditionBuilder{TSource}"/>.
/// </summary>
public sealed class KeyConditionExpressionResult
{
    /// <summary>
    /// The DynamoDB KeyConditionExpression string.
    /// E.g. "#key_0 = :key_v0 AND #key_1 > :key_v1"
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// Attribute name aliases for reserved keywords.
    /// E.g. { "#key_0": "PK", "#key_1": "SK" }
    /// </summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>
    /// Attribute value placeholders.
    /// E.g. { ":key_v0": { S: "USER#123" }, ":key_v1": { S: "ORDER#2024" } }
    /// </summary>
    public IReadOnlyDictionary<string, AttributeValue> ExpressionAttributeValues { get; }

    /// <summary>
    /// Creates a new key condition expression result.
    /// </summary>
    /// <param name="expression">The DynamoDB KeyConditionExpression string.</param>
    /// <param name="expressionAttributeNames">Attribute name aliases.</param>
    /// <param name="expressionAttributeValues">Attribute value placeholders.</param>
    internal KeyConditionExpressionResult(
        string expression,
        IReadOnlyDictionary<string, string> expressionAttributeNames,
        IReadOnlyDictionary<string, AttributeValue> expressionAttributeValues)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        ExpressionAttributeNames = expressionAttributeNames ?? throw new ArgumentNullException(nameof(expressionAttributeNames));
        ExpressionAttributeValues = expressionAttributeValues ?? throw new ArgumentNullException(nameof(expressionAttributeValues));
    }
}
