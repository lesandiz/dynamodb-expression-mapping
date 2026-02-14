using System.Reflection;
using Amazon.DynamoDBv2.Model;
using DynamoDb.ExpressionMapping.Attributes;
using DynamoDb.ExpressionMapping.Exceptions;

namespace DynamoDb.ExpressionMapping.Mapping;

/// <summary>
/// Converts .NET values to DynamoDB AttributeValue for use in expression builders.
/// Shared by FilterExpressionBuilder, UpdateExpressionBuilder, ConditionExpressionBuilder,
/// and KeyConditionExpressionBuilder to ensure consistent converter resolution.
/// </summary>
internal sealed class ExpressionValueEmitter
{
    private readonly IAttributeValueConverterRegistry registry;

    public ExpressionValueEmitter(IAttributeValueConverterRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Converts a .NET value to an AttributeValue, using the converter resolved
    /// for the given property. Applies the resolution order from Section 8:
    /// [DynamoDbConverter] on property → registry exact match → Nullable → Enum →
    /// open-generic collection → MissingConverterException.
    /// </summary>
    /// <param name="value">The .NET value to convert.</param>
    /// <param name="property">
    /// The PropertyInfo of the expression property being compared/set.
    /// Used to check for [DynamoDbConverter] attribute override (Section 8, step 1).
    /// May be null for literal values not tied to a property (e.g. Between bounds),
    /// in which case step 1 is skipped and resolution starts at step 2 using the
    /// runtime type of <paramref name="value"/>.
    /// </param>
    /// <returns>The DynamoDB AttributeValue representation.</returns>
    /// <exception cref="MissingConverterException">
    /// No converter found for the value's type.
    /// </exception>
    public AttributeValue Emit(object value, PropertyInfo? property)
    {
        var converter = ResolveConverter(value, property);
        return converter.ToAttributeValue(value);
    }

    private IAttributeValueConverter ResolveConverter(object value, PropertyInfo? property)
    {
        // Step 1: Check [DynamoDbConverter] attribute on the property
        if (property != null)
        {
            var attr = property.GetCustomAttribute<DynamoDbConverterAttribute>();
            if (attr != null)
            {
                return (IAttributeValueConverter)Activator.CreateInstance(attr.ConverterType)!;
            }
        }

        // Steps 2–6: Delegate to registry (exact → Nullable → Enum → collection → throw)
        var targetType = property?.PropertyType ?? value.GetType();
        return registry.GetConverter(targetType);
    }
}
