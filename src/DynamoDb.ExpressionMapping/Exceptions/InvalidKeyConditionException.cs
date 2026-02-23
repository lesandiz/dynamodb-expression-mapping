using System.Diagnostics.CodeAnalysis;

namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when a key condition expression references an ignored property
/// or a nested property path (key attributes must be top-level).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidKeyConditionException : InvalidExpressionException
{
    public InvalidKeyConditionException(string message, string? propertyName = null, Type? entityType = null)
        : base(message, propertyName, entityType)
    { }
}
