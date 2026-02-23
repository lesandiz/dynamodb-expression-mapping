using System.Diagnostics.CodeAnalysis;

namespace DynamoDb.ExpressionMapping.Attributes;

/// <summary>
/// Maps a C# property to a DynamoDB attribute with a different name.
/// When absent, the property name is used as-is (convention-based default).
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DynamoDbAttributeAttribute : Attribute
{
    public string AttributeName { get; }

    public DynamoDbAttributeAttribute(string attributeName)
    {
        ArgumentException.ThrowIfNullOrEmpty(attributeName);
        AttributeName = attributeName;
    }
}

/// <summary>
/// Marks a property as not stored in DynamoDB.
/// Projecting this property will either:
/// - Throw at build time (strict mode, default)
/// - Be silently excluded (lenient mode)
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DynamoDbIgnoreAttribute : Attribute { }

/// <summary>
/// Specifies a custom converter for this property's AttributeValue serialisation.
/// </summary>
[ExcludeFromCodeCoverage]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DynamoDbConverterAttribute : Attribute
{
    public Type ConverterType { get; }

    public DynamoDbConverterAttribute(Type converterType)
    {
        ArgumentNullException.ThrowIfNull(converterType);
        ConverterType = converterType;
    }
}
