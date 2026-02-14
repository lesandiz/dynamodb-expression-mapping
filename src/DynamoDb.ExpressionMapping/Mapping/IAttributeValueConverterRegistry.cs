namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Registry of type converters. Thread-safe, supports runtime registration.
/// </summary>
public interface IAttributeValueConverterRegistry
{
    /// <summary>
    /// Gets the converter for a .NET type.
    /// </summary>
    /// <exception cref="Exceptions.MissingConverterException">No converter registered for the type.</exception>
    IAttributeValueConverter GetConverter(Type type);

    /// <summary>
    /// Gets a strongly-typed converter.
    /// </summary>
    IAttributeValueConverter<T> GetConverter<T>();

    /// <summary>
    /// Checks if a converter is registered for the type.
    /// </summary>
    bool HasConverter(Type type);

    /// <summary>
    /// Registers a custom converter. Overwrites any existing converter for the same type.
    /// </summary>
    void Register<T>(IAttributeValueConverter<T> converter);
}
