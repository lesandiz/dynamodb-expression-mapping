using System.Diagnostics.CodeAnalysis;

namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Base class for all exceptions thrown by DynamoDb.ExpressionMapping.
/// Catch this type at API boundaries for blanket handling.
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class ExpressionMappingException : Exception
{
    protected ExpressionMappingException(string message)
        : base(message) { }

    protected ExpressionMappingException(string message, Exception innerException)
        : base(message, innerException) { }
}
