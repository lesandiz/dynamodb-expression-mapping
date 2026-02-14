using System.Linq.Expressions;

namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when an expression tree contains a node type that cannot
/// be translated to a DynamoDB expression (e.g. method calls,
/// arithmetic, conditional, array indexing).
/// </summary>
public sealed class UnsupportedExpressionException : ExpressionMappingException
{
    /// <summary>
    /// The expression tree node type that was rejected.
    /// E.g. <see cref="System.Linq.Expressions.ExpressionType.Call"/>.
    /// </summary>
    public ExpressionType NodeType { get; }

    /// <summary>
    /// The <c>.ToString()</c> representation of the rejected expression node,
    /// for diagnostic purposes.
    /// </summary>
    public string ExpressionText { get; }

    public UnsupportedExpressionException(ExpressionType nodeType, string expressionText)
        : base($"Expression node type '{nodeType}' is not supported: {expressionText}")
    {
        NodeType = nodeType;
        ExpressionText = expressionText;
    }
}
