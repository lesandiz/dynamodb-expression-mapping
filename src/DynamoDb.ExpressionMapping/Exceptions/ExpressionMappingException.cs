namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Base class for all exceptions thrown by DynamoDb.ExpressionMapping.
/// Catch this type at API boundaries for blanket handling.
/// </summary>
public abstract class ExpressionMappingException : Exception
{
    protected ExpressionMappingException(string message)
        : base(message) { }

    protected ExpressionMappingException(string message, Exception innerException)
        : base(message, innerException) { }
}
