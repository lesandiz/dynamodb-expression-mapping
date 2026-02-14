using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Generic converter for List&lt;T&gt;.
/// Wraps an element converter and converts to/from DynamoDB List (L) attribute.
/// Returns empty list if attribute is missing or null.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class ListConverter<T> : AttributeValueConverterBase<List<T>>
{
    private readonly IAttributeValueConverter<T> elementConverter;

    public ListConverter(IAttributeValueConverter<T> elementConverter)
    {
        this.elementConverter = elementConverter ?? throw new ArgumentNullException(nameof(elementConverter));
    }

    public override List<T> FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue?.L == null)
            return new List<T>();

        return attributeValue.L
            .Select(e => elementConverter.FromAttributeValue(e))
            .ToList();
    }

    public override AttributeValue ToAttributeValue(List<T> value)
    {
        return new AttributeValue
        {
            L = value
                .Select(e => elementConverter.ToAttributeValue(e))
                .ToList()
        };
    }
}
