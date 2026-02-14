using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between DateTimeOffset and DynamoDB String (S) attribute using ISO 8601 format.
/// DateTimeOffset.MinValue is returned if attribute is missing.
/// </summary>
internal sealed class DateTimeOffsetConverter : AttributeValueConverterBase<DateTimeOffset>
{
    public override AttributeValue ToAttributeValue(DateTimeOffset value)
    {
        return new AttributeValue { S = value.ToString("O") };
    }

    public override DateTimeOffset FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || string.IsNullOrEmpty(attributeValue.S))
            return DateTimeOffset.MinValue;

        return DateTimeOffset.Parse(attributeValue.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
