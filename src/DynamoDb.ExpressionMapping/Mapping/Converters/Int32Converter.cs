using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between int and DynamoDB Number (N) attribute.
/// Zero is returned if attribute is missing.
/// </summary>
internal sealed class Int32Converter : AttributeValueConverterBase<int>
{
    public override AttributeValue ToAttributeValue(int value)
    {
        return new AttributeValue { N = value.ToString() };
    }

    public override int FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || string.IsNullOrEmpty(attributeValue.N))
            return 0;

        return int.Parse(attributeValue.N);
    }
}
