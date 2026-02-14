using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between byte[] and DynamoDB Binary (B) attribute.
/// Null is returned if attribute is missing.
/// </summary>
internal sealed class ByteArrayConverter : AttributeValueConverterBase<byte[]>
{
    public override AttributeValue ToAttributeValue(byte[] value)
    {
        if (value == null)
            return new AttributeValue { NULL = true };

        return new AttributeValue { B = new MemoryStream(value) };
    }

    public override byte[] FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || attributeValue.B == null)
            return null!;

        return attributeValue.B.ToArray();
    }
}
