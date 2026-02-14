using Amazon.DynamoDBv2.Model;

namespace DynamoDb.ExpressionMapping.Mapping.Converters;

/// <summary>
/// Generic wrapper converter for Nullable&lt;T&gt; where T : struct.
/// Wraps an inner converter for the underlying value type.
/// Returns null if AttributeValue is null or NULL = true.
/// Writes { NULL = true } if value has no value.
/// </summary>
/// <typeparam name="T">The underlying value type.</typeparam>
internal sealed class NullableConverter<T> : AttributeValueConverterBase<T?>
    where T : struct
{
    private readonly IAttributeValueConverter<T> inner;

    public NullableConverter(IAttributeValueConverter<T> inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public override T? FromAttributeValue(AttributeValue attributeValue)
    {
        if (attributeValue == null || attributeValue.NULL)
            return null;

        return inner.FromAttributeValue(attributeValue);
    }

    public override AttributeValue ToAttributeValue(T? value)
    {
        if (!value.HasValue)
            return new AttributeValue { NULL = true };

        return inner.ToAttributeValue(value.Value);
    }
}
