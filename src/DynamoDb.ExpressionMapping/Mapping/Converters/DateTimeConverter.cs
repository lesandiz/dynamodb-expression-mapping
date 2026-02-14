using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between DateTime and DynamoDB String (S) attribute using ISO 8601 format.
/// DateTime.MinValue is returned if attribute is missing.
/// </summary>
internal sealed class DateTimeConverter : AttributeValueConverterBase<DateTime>
{
    public override AttributeValue ToAttributeValue(DateTime value)
    {
        return new AttributeValue { S = value.ToString("O") };
    }

    public override DateTime FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || string.IsNullOrEmpty(attributeValue.S))
            return DateTime.MinValue;

        return DateTime.Parse(attributeValue.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
