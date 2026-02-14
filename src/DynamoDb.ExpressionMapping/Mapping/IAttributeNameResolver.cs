using DynamoDb.ExpressionMapping.Exceptions;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Resolves C# property names to DynamoDB attribute names and vice versa.
/// </summary>
public interface IAttributeNameResolver
{
    /// <summary>
    /// Gets the DynamoDB attribute name for a C# property.
    /// </summary>
    /// <exception cref="InvalidProjectionException">
    /// Thrown when the property is marked with [DynamoDbIgnore] in strict mode (Spec 14).
    /// </exception>
    string GetAttributeName(string propertyName);

    /// <summary>
    /// Returns whether a property represents a stored DynamoDB attribute.
    /// False for computed properties marked with [DynamoDbIgnore].
    /// </summary>
    bool IsStoredAttribute(string propertyName);

    /// <summary>
    /// Gets the C# property name for a DynamoDB attribute name (reverse lookup).
    /// Used during result mapping.
    /// </summary>
    string GetPropertyName(string attributeName);
}

/// <summary>
/// Generic resolver that inspects type T for attribute annotations.
/// </summary>
public interface IAttributeNameResolver<T> : IAttributeNameResolver { }
