namespace DynamoDb.ExpressionMapping.Exceptions;

/// <summary>
/// Thrown when no <see cref="IAttributeValueConverter"/> is registered for a
/// .NET type. Thrown at mapper/builder creation time (fail fast), not during
/// query execution.
/// </summary>
public sealed class MissingConverterException : ExpressionMappingException
{
    /// <summary>
    /// The .NET type for which no converter was found.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// The property that triggered the converter lookup, if known.
    /// Null when the lookup originated from a direct registry call.
    /// </summary>
    public string? PropertyName { get; }

    public MissingConverterException(Type targetType, string? propertyName = null)
        : base(propertyName is not null
            ? $"No converter registered for type '{targetType}' (property '{propertyName}')."
            : $"No converter registered for type '{targetType}'.")
    {
        TargetType = targetType;
        PropertyName = propertyName;
    }
}
