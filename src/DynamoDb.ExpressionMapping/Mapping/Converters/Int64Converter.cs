using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between long and DynamoDB Number (N) attribute.
/// Zero is returned if attribute is missing.
/// </summary>
internal sealed class Int64Converter : AttributeValueConverterBase<long>
{
    public override AttributeValue ToAttributeValue(long value)
    {
        return new AttributeValue { N = value.ToString() };
    }

    public override long FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || string.IsNullOrEmpty(attributeValue.N))
            return 0L;

        return long.Parse(attributeValue.N);
    }
}
