using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Generic converter for HashSet&lt;T&gt;.
/// Uses native DynamoDB String Set (SS) or Number Set (NS) when the element converter
/// produces S or N attribute values. Falls back to List (L) for complex types.
/// Returns empty set if attribute is missing or null.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class SetConverter<T> : AttributeValueConverterBase<HashSet<T>>
{
    private readonly IAttributeValueConverter<T> elementConverter;

    public SetConverter(IAttributeValueConverter<T> elementConverter)
    {
        this.elementConverter = elementConverter ?? throw new ArgumentNullException(nameof(elementConverter));
    }

    public override HashSet<T> FromAttributeValue(AttributeValue attributeValue)
    {
        // Try native SS (String Set)
        if (attributeValue?.SS != null && attributeValue.SS.Count > 0)
        {
            return attributeValue.SS
                .Select(s => elementConverter.FromAttributeValue(new AttributeValue { S = s }))
                .ToHashSet();
        }

        // Try native NS (Number Set)
        if (attributeValue?.NS != null && attributeValue.NS.Count > 0)
        {
            return attributeValue.NS
                .Select(n => elementConverter.FromAttributeValue(new AttributeValue { N = n }))
                .ToHashSet();
        }

        // Fall back to List (for complex types)
        if (attributeValue?.L != null)
        {
            return attributeValue.L
                .Select(e => elementConverter.FromAttributeValue(e))
                .ToHashSet();
        }

        return new HashSet<T>();
    }

    public override AttributeValue ToAttributeValue(HashSet<T> value)
    {
        if (value.Count == 0)
            return new AttributeValue { L = new List<AttributeValue>() };

        // Probe the element converter's output to determine native set format.
        // If elements serialise to S → use SS; if N → use NS; otherwise → L.
        var sample = elementConverter.ToAttributeValue(value.First());

        if (sample.S != null)
        {
            return new AttributeValue
            {
                SS = value
                    .Select(e => elementConverter.ToAttributeValue(e).S)
                    .ToList()
            };
        }

        if (sample.N != null)
        {
            return new AttributeValue
            {
                NS = value
                    .Select(e => elementConverter.ToAttributeValue(e).N)
                    .ToList()
            };
        }

        return new AttributeValue
        {
            L = value
                .Select(e => elementConverter.ToAttributeValue(e))
                .ToList()
        };
    }
}
