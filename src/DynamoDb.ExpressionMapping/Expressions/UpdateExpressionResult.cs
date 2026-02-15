using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Expressions;

/// <summary>
/// Represents a compiled DynamoDB UpdateExpression with attribute name and value mappings.
/// Immutable result object returned by <see cref="IUpdateExpressionBuilder{TSource}.Build"/>.
/// </summary>
public sealed class UpdateExpressionResult
{
    /// <summary>
    /// The DynamoDB UpdateExpression string.
    /// E.g. "SET #upd_0 = :upd_v0, #upd_1 = #upd_1 + :upd_v1 REMOVE #upd_2"
    /// </summary>
    public string Expression { get; }

    /// <summary>Attribute name aliases.</summary>
    public IReadOnlyDictionary<string, string> ExpressionAttributeNames { get; }

    /// <summary>Attribute value placeholders.</summary>
    public IReadOnlyDictionary<string, AttributeValue> ExpressionAttributeValues { get; }

    /// <summary>Whether no operations were added.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(Expression);

    /// <summary>
    /// Empty result singleton.
    /// </summary>
    public static UpdateExpressionResult Empty { get; } = new(
        string.Empty,
        new Dictionary<string, string>(),
        new Dictionary<string, AttributeValue>());

    /// <summary>
    /// Creates a new update expression result.
    /// </summary>
    /// <param name="expression">The DynamoDB UpdateExpression string.</param>
    /// <param name="names">Attribute name aliases.</param>
    /// <param name="values">Attribute value placeholders.</param>
    public UpdateExpressionResult(
        string expression,
        IReadOnlyDictionary<string, string> names,
        IReadOnlyDictionary<string, AttributeValue> values)
    {
        Expression = expression ?? string.Empty;
        ExpressionAttributeNames = names ?? throw new ArgumentNullException(nameof(names));
        ExpressionAttributeValues = values ?? throw new ArgumentNullException(nameof(values));
    }
}
