using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Generic converter for Dictionary&lt;string, TValue&gt;.
/// Wraps a value converter and converts to/from DynamoDB Map (M) attribute.
/// Returns empty dictionary if attribute is missing or null.
/// </summary>
/// <typeparam name="TValue">The dictionary value type.</typeparam>
internal sealed class MapConverter<TValue> : AttributeValueConverterBase<Dictionary<string, TValue>>
{
    private readonly IAttributeValueConverter<TValue> valueConverter;

    public MapConverter(IAttributeValueConverter<TValue> valueConverter)
    {
        this.valueConverter = valueConverter ?? throw new ArgumentNullException(nameof(valueConverter));
    }

    public override Dictionary<string, TValue> FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue?.M == null)
            return new Dictionary<string, TValue>();

        return attributeValue.M.ToDictionary(
            kvp => kvp.Key,
            kvp => valueConverter.FromAttributeValue(kvp.Value));
    }

    public override AttributeValue ToAttributeValue(Dictionary<string, TValue> value)
    {
        return new AttributeValue
        {
            M = value.ToDictionary(
                kvp => kvp.Key,
                kvp => valueConverter.ToAttributeValue(kvp.Value))
        };
    }
}
