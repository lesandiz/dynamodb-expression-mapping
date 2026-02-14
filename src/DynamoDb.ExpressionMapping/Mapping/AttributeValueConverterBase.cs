using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Base class for type-safe converters, providing non-generic interface implementation.
/// </summary>
/// <typeparam name="T">The .NET type this converter handles.</typeparam>
public abstract class AttributeValueConverterBase<T> : IAttributeValueConverter<T>
{
    public Type TargetType => typeof(T);

    public abstract AttributeValue ToAttributeValue(T value);
    public abstract T FromAttributeValue(AttributeValue attributeValue);

    // Non-generic interface implementation (boxing for reference types, but required for registry)
    AttributeValue IAttributeValueConverter.ToAttributeValue(object value)
    {
        return ToAttributeValue((T)value);
    }

    object IAttributeValueConverter.FromAttributeValue(AttributeValue attributeValue)
    {
        return FromAttributeValue(attributeValue)!;
    }
}
