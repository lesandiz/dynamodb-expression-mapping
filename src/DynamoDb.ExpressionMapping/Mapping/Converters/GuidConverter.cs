using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between Guid and DynamoDB String (S) attribute.
/// Guid.Empty is returned if attribute is missing.
/// </summary>
internal sealed class GuidConverter : AttributeValueConverterBase<Guid>
{
    public override AttributeValue ToAttributeValue(Guid value)
    {
        return new AttributeValue { S = value.ToString() };
    }

    public override Guid FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || string.IsNullOrEmpty(attributeValue.S))
            return Guid.Empty;

        return Guid.Parse(attributeValue.S);
    }
}
