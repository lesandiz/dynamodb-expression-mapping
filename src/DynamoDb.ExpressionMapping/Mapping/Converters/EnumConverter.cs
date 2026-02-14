using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Generic converter for all enum types.
/// Supports string (default) or numeric storage mode.
/// String mode uses Enum.Parse with ignoreCase: true.
/// Number mode casts to/from int.
/// </summary>
/// <typeparam name="TEnum">The enum type to convert.</typeparam>
internal sealed class EnumConverter<TEnum> : AttributeValueConverterBase<TEnum>
    where TEnum : struct, Enum
{
    private readonly EnumStorageMode mode;

    public EnumConverter(EnumStorageMode mode = EnumStorageMode.String)
    {
        this.mode = mode;
    }

    public override TEnum FromAttributeValue(AttributeValue attributeValue)
    {
        return mode switch
        {
            EnumStorageMode.String => Enum.Parse<TEnum>(attributeValue.S, ignoreCase: true),
            EnumStorageMode.Number => (TEnum)(object)int.Parse(attributeValue.N),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid EnumStorageMode")
        };
    }

    public override AttributeValue ToAttributeValue(TEnum value)
    {
        return mode switch
        {
            EnumStorageMode.String => new AttributeValue { S = value.ToString() },
            EnumStorageMode.Number => new AttributeValue { N = ((int)(object)value).ToString() },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid EnumStorageMode")
        };
    }
}
