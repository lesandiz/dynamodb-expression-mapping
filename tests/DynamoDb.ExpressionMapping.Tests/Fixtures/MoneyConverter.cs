using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Mapping;

namespace DynamoDb.ExpressionMapping.Tests.Fixtures;

/// <summary>
/// Custom converter for Money type used in integration tests.
/// Stores Money as a DynamoDB Map with "Amount" (N) and "Currency" (S).
/// </summary>
public class MoneyConverter : AttributeValueConverterBase<Money>
{
    public override AttributeValue ToAttributeValue(Money value)
    {
        return new AttributeValue
        {
            M = new Dictionary<string, AttributeValue>
            {
                ["Amount"] = new() { N = value.Amount.ToString("F2") },
                ["Currency"] = new() { S = value.Currency }
            }
        };
    }

    public override Money FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue?.M == null)
            return new Money(0m, "USD");

        var amount = attributeValue.M.TryGetValue("Amount", out var amountAttr) && amountAttr.N != null
            ? decimal.Parse(amountAttr.N)
            : 0m;

        var currency = attributeValue.M.TryGetValue("Currency", out var currencyAttr) && currencyAttr.S != null
            ? currencyAttr.S
            : "USD";

        return new Money(amount, currency);
    }
}
