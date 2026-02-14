using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between string and DynamoDB String (S) attribute.
/// Null values are represented as NULL=true or missing attribute.
/// </summary>
internal sealed class StringConverter : AttributeValueConverterBase<string>
{
    public override AttributeValue ToAttributeValue(string value)
    {
        if (value == null)
            return new AttributeValue { NULL = true };

        return new AttributeValue { S = value };
    }

    public override string FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL)
            return null!;

        return attributeValue.S;
    }
}
