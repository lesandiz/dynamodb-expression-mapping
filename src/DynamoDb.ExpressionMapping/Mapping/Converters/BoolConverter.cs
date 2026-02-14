using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between bool and DynamoDB BOOL attribute.
/// False is returned if attribute is missing.
/// </summary>
internal sealed class BoolConverter : AttributeValueConverterBase<bool>
{
    public override AttributeValue ToAttributeValue(bool value)
    {
        return new AttributeValue { BOOL = value };
    }

    public override bool FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL)
            return false;

        return attributeValue.BOOL;
    }
}
