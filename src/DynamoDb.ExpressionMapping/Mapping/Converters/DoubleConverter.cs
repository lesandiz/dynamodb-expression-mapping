using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Converts between double and DynamoDB Number (N) attribute.
/// Zero is returned if attribute is missing.
/// </summary>
internal sealed class DoubleConverter : AttributeValueConverterBase<double>
{
    public override AttributeValue ToAttributeValue(double value)
    {
        return new AttributeValue { N = value.ToString() };
    }

    public override double FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL || string.IsNullOrEmpty(attributeValue.N))
            return 0.0;

        return double.Parse(attributeValue.N);
    }
}
