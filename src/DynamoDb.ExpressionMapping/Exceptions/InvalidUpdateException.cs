using System.Diagnostics.CodeAnalysis;

namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when an update expression targets an ignored property or contains
/// conflicting operations on the same attribute (e.g. SET + REMOVE).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class InvalidUpdateException : InvalidExpressionException
{
    public InvalidUpdateException(string message, string? propertyName = null, Type? entityType = null)
        : base(message, propertyName, entityType)
    { }
}
