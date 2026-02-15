using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts T[] arrays to/from DynamoDB List (L) attributes.
/// Uses the element converter for individual items.
/// </summary>
internal sealed class ArrayConverter<T> : AttributeValueConverterBase<T[]>
{
    private readonly IAttributeValueConverter<T> _elementConverter;

    public ArrayConverter(IAttributeValueConverter<T> elementConverter)
    {
        _elementConverter = elementConverter ?? throw new ArgumentNullException(nameof(elementConverter));
    }

    public override AttributeValue ToAttributeValue(T[] value)
    {
        if (value == null || value.Length == 0)
            return new AttributeValue { NULL = true };

        var list = new List<AttributeValue>(value.Length);
        foreach (var item in value)
        {
            list.Add(_elementConverter.ToAttributeValue(item));
        }

        return new AttributeValue { L = list };
    }

    public override T[] FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue?.L == null || attributeValue.L.Count == 0)
            return Array.Empty<T>();

        var array = new T[attributeValue.L.Count];
        for (int i = 0; i < attributeValue.L.Count; i++)
        {
            array[i] = _elementConverter.FromAttributeValue(attributeValue.L[i]);
        }

        return array;
    }
}
