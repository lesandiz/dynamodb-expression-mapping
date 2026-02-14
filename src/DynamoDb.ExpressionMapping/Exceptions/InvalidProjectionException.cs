namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when a projection expression references a property that cannot
/// be projected (e.g. marked with <c>[DynamoDbIgnore]</c> in strict mode).
/// </summary>
public sealed class InvalidProjectionException : InvalidExpressionException
{
    public InvalidProjectionException(string propertyName, Type entityType)
        : base(
            $"Cannot project property '{propertyName}' on '{entityType.Name}': " +
            "property is marked [DynamoDbIgnore] or is not a stored attribute.",
            propertyName,
            entityType)
    { }
}
