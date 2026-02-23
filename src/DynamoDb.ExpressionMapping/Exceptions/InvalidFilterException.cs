using System.Diagnostics.CodeAnalysis;

namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when a filter or condition predicate references an ignored property
/// (in strict mode) or is not a boolean expression.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidFilterException : InvalidExpressionException
{
    public InvalidFilterException(string message, string? propertyName = null, Type? entityType = null)
        : base(message, propertyName, entityType)
    { }
}
