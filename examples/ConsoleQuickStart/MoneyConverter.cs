using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;

namespace ConsoleQuickStart;

public class MoneyConverter : IAttributeValueConverter<Money>
{
    public Type TargetType => typeof(Money);

    public AttributeValue ToAttributeValue(Money value) => new()
    {
        IsMSet = true,
        M = new Dictionary<string, AttributeValue>
        {
            ["Amount"] = new AttributeValue { N = value.Amount.ToString() },
            ["Currency"] = new AttributeValue { S = value.Currency }
        }
    };

    public Money FromAttributeValue(AttributeValue attributeValue)
    {
        var map = attributeValue.M;
        return new Money(
            decimal.Parse(map["Amount"].N),
            map["Currency"].S);
    }

    // Explicit interface implementation for non-generic interface
    AttributeValue IAttributeValueConverter.ToAttributeValue(object value)
        => ToAttributeValue((Money)value);

    object IAttributeValueConverter.FromAttributeValue(AttributeValue attributeValue)
        => FromAttributeValue(attributeValue);
}
