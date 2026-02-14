using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between decimal and DynamoDB Number (N) attribute.
/// Zero is returned if attribute is missing.
/// </summary>
internal sealed class DecimalConverter : AttributeValueConverterBase<decimal>
{
    public override AttributeValue ToAttributeValue(decimal value)
    {
        return new AttributeValue { N = value.ToString() };
    }

    public override decimal FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || string.IsNullOrEmpty(attributeValue.N))
            return 0m;

        return decimal.Parse(attributeValue.N);
    }
}
