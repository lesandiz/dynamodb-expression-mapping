namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Base class for exceptions thrown when a builder detects an invalid
/// expression input (e.g. ignored properties, non-boolean filters,
/// conflicting update clauses). Catch this type at builder boundaries.
/// </summary>
public abstract class InvalidExpressionException : ExpressionMappingException
{
    /// <summary>
    /// The C# property name that caused the validation failure, if applicable.
    /// </summary>
    public string? PropertyName { get; }

    /// <summary>
    /// The entity type being processed when the error occurred.
    /// </summary>
    public Type? EntityType { get; }

    /// <summary>
    /// The resolved DynamoDB attribute name, if resolution succeeded before
    /// the error was detected. Null when the error prevented resolution.
    /// </summary>
    public string? AttributeName { get; }

    protected InvalidExpressionException(
        string message,
        string? propertyName = null,
        Type? entityType = null,
        string? attributeName = null)
        : base(message)
    {
        PropertyName = propertyName;
        EntityType = entityType;
        AttributeName = attributeName;
    }
}
