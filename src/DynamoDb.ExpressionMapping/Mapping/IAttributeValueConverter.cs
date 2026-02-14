using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Converts between .NET types and DynamoDB AttributeValue.
/// Implementations are stateless and thread-safe.
/// </summary>
public interface IAttributeValueConverter
{
    /// <summary>
    /// The .NET type this converter handles.
    /// </summary>
    Type TargetType { get; }

    /// <summary>
    /// Converts a .NET value to a DynamoDB AttributeValue.
    /// </summary>
    AttributeValue ToAttributeValue(object value);

    /// <summary>
    /// Converts a DynamoDB AttributeValue to a .NET value.
    /// </summary>
    object FromAttributeValue(AttributeValue attributeValue);
}

/// <summary>
/// Strongly-typed converter interface for compile-time safety
/// and avoiding boxing for value types.
/// </summary>
/// <typeparam name="T">The .NET type this converter handles.</typeparam>
public interface IAttributeValueConverter<T> : IAttributeValueConverter
{
    /// <summary>
    /// Converts a strongly-typed .NET value to a DynamoDB AttributeValue.
    /// </summary>
    new AttributeValue ToAttributeValue(T value);

    /// <summary>
    /// Converts a DynamoDB AttributeValue to a strongly-typed .NET value.
    /// </summary>
    new T FromAttributeValue(AttributeValue attributeValue);
}
